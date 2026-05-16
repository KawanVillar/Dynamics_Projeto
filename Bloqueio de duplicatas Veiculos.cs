using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;

namespace Dynamics_Projeto
{
    public sealed class OpportunityBloqueioDuplicidadeVeiculoPlugin : IPlugin
    {
        private const string EntityLogicalName = "opportunity";
        private const string VehicleLookupAttribute = "klima_veiculos";
        private const string DuplicateMessage = "Já existe uma oportunidade aberta para este veículo. Não é permitido duplicidade de reservas.";

        public void Execute(IServiceProvider serviceProvider)
        {
            if (serviceProvider == null) throw new InvalidPluginExecutionException(nameof(serviceProvider));

            var tracing = (ITracingService)serviceProvider.GetService(typeof(ITracingService));
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));

            tracing.Trace("Iniciando Plugin de Duplicidade...");

            // Validação de Contexto
            if (context == null || !IsSupportedContext(context))
            {
                tracing.Trace("Contexto não suportado ou entidade incorreta.");
                return;
            }

            if (!(context.InputParameters["Target"] is Entity target)) return;

            EntityReference vehicleRef = null;

            // 1. Prioridade: Valor no Target (Criação ou alteração do campo veículo)
            if (target.Contains(VehicleLookupAttribute))
            {
                vehicleRef = target.GetAttributeValue<EntityReference>(VehicleLookupAttribute);
                tracing.Trace("Veículo encontrado no Target.");
            }
            // 2. Fallback: Valor na Pre-Image (Update de outros campos onde o veículo já estava preenchido)
            else if (context.MessageName.Equals("Update", StringComparison.OrdinalIgnoreCase))
            {
                vehicleRef = TryGetVehicleFromPreImages(context);
                tracing.Trace(vehicleRef != null ? "Veículo recuperado da Pre-Image." : "Veículo não encontrado na operação.");
            }

            // Se não houver veículo envolvido, encerra a execução
            if (vehicleRef == null || vehicleRef.Id == Guid.Empty)
            {
                tracing.Trace("Nenhum veículo associado. Fim da execução.");
                return;
            }

            var factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            var orgService = factory.CreateOrganizationService(context.UserId);

            ValidarEBloquear(orgService, context.PrimaryEntityId, vehicleRef.Id, tracing);
        }

        private static bool IsSupportedContext(IPluginExecutionContext context)
        {
            return (context.MessageName.Equals("Create", StringComparison.OrdinalIgnoreCase) || 
                    context.MessageName.Equals("Update", StringComparison.OrdinalIgnoreCase)) &&
                    context.Stage == 20 && 
                    context.PrimaryEntityName.Equals(EntityLogicalName, StringComparison.OrdinalIgnoreCase);
        }

        private static EntityReference TryGetVehicleFromPreImages(IPluginExecutionContext context)
        {
            if (context.PreEntityImages != null && context.PreEntityImages.Contains("PreImage"))
            {
                return context.PreEntityImages["PreImage"].GetAttributeValue<EntityReference>(VehicleLookupAttribute);
            }
            return null;
        }

        private static void ValidarEBloquear(IOrganizationService service, Guid currentId, Guid vehicleId, ITracingService tracing)
        {
            var query = new QueryExpression(EntityLogicalName)
            {
                ColumnSet = new ColumnSet("opportunityid"),
                TopCount = 1
            };

            // Critério 1: Oportunidades com o mesmo veículo
            query.Criteria.AddCondition(VehicleLookupAttribute, ConditionOperator.Equal, vehicleId);
            
            // Critério 2: APENAS oportunidades que ainda estão ABERTAS (statecode = 0)
            // Isso resolve o problema de permitir novos registros após uma oportunidade ser "Lost" ou "Canceled"
            query.Criteria.AddCondition("statecode", ConditionOperator.Equal, 0); 

            // Critério 3: Se for Update, ignorar o próprio registro que está sendo editado
            if (currentId != Guid.Empty)
            {
                query.Criteria.AddCondition("opportunityid", ConditionOperator.NotEqual, currentId);
            }

            var result = service.RetrieveMultiple(query);
            tracing.Trace($"Busca concluída. Oportunidades abertas encontradas: {result.Entities.Count}");

            if (result.Entities.Count > 0)
            {
                tracing.Trace("Bloqueando: Reserva duplicada detectada.");
                throw new InvalidPluginExecutionException(DuplicateMessage);
            }
        }
    }
}