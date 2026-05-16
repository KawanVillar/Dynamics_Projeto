using Microsoft.Xrm.Sdk;
using System;
using System.Net.Mail;
using System.Text.RegularExpressions;

namespace Dynamics_Projeto
{
    public sealed class ContactPreOperationValidationPlugin : IPlugin
    {
        private const string EntityLogicalName = "contact";

        private const string MessageCreate = "Create";
        private const string MessageUpdate = "Update";

        // Dataverse pipeline stages: 10=PreValidation, 20=PreOperation, 40=PostOperation.
        private const int StagePreOperation = 20;

        // Execution modes: 0=Sync, 1=Async.
        private const int ModeSynchronous = 0;

        private static readonly Regex TelephoneRegex = new Regex(
            @"^\d{10,11}$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public void Execute(IServiceProvider serviceProvider)
        {
            if (serviceProvider == null)
            {
                throw new InvalidPluginExecutionException(nameof(serviceProvider));
            }

            var tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));

            if (context == null)
            {
                tracingService?.Trace("IPluginExecutionContext está nulo.");
                return;
            }

            if (!IsSupportedContext(context))
            {
                return;
            }

            if (!context.InputParameters.Contains("Target"))
            {
                tracingService?.Trace("InputParameters não contém 'Target'.");
                return;
            }

            if (!(context.InputParameters["Target"] is Entity target))
            {
                tracingService?.Trace("InputParameters['Target'] não é do tipo Entity.");
                return;
            }

            if (!string.Equals(target.LogicalName, EntityLogicalName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var isCreate = string.Equals(context.MessageName, MessageCreate, StringComparison.OrdinalIgnoreCase);
            var isUpdate = !isCreate;

            // No Update, evita processamento se nenhum dos campos de validação veio no Target.
            // (Em updates, o Target normalmente contém apenas os campos alterados.)
            if (isUpdate && !HasAnyRelevantAttribute(target))
            {
                return;
            }

            // firstname: obrigatório
            // - Create: sempre valida (deve vir no Target)
            // - Update: valida apenas se o atributo estiver sendo alterado
            if (isCreate || target.Contains("firstname"))
            {
                ValidateRequiredString(target, "firstname", "O campo Nome (firstname) é obrigatório.", tracingService);
            }

            // emailaddress1: formato válido (quando fornecido)
            if (target.Contains("emailaddress1"))
            {
                ValidateEmail(target, "emailaddress1", tracingService);
            }

            // telephone1: regex ^\d{10,11}$ (quando fornecido)
            if (target.Contains("telephone1"))
            {
                ValidateTelephone(target, "telephone1", tracingService);
            }
        }

        private static bool IsSupportedContext(IPluginExecutionContext context)
        {
            if (!string.Equals(context.PrimaryEntityName, EntityLogicalName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!string.Equals(context.MessageName, MessageCreate, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(context.MessageName, MessageUpdate, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (context.Stage != StagePreOperation)
            {
                return false;
            }

            if (context.Mode != ModeSynchronous)
            {
                return false;
            }

            return true;
        }

        private static bool HasAnyRelevantAttribute(Entity target)
        {
            return target.Contains("firstname") || target.Contains("emailaddress1") || target.Contains("telephone1");
        }

        private static void ValidateRequiredString(Entity target, string attributeName, string errorMessage, ITracingService tracing)
        {
            if (!target.Contains(attributeName))
            {
                tracing?.Trace("Validação falhou: atributo ausente no Target. Atributo: {0}", attributeName);
                throw new InvalidPluginExecutionException(errorMessage);
            }

            var value = target.GetAttributeValue<string>(attributeName);

            if (string.IsNullOrWhiteSpace(value))
            {
                tracing?.Trace("Validação falhou: atributo obrigatório vazio. Atributo: {0}", attributeName);
                throw new InvalidPluginExecutionException(errorMessage);
            }
        }

        private static void ValidateEmail(Entity target, string attributeName, ITracingService tracing)
        {
            if (!target.Contains(attributeName))
            {
                return;
            }

            var email = target.GetAttributeValue<string>(attributeName);

            // Não é obrigatório: se estiver vazio/nulo, não valida formato.
            if (string.IsNullOrWhiteSpace(email))
            {
                return;
            }

            email = email.Trim();

            if (!IsValidEmail(email))
            {
                tracing?.Trace("Validação falhou: email inválido. Atributo: {0}. Valor: '{1}'", attributeName, email);
                throw new InvalidPluginExecutionException("O campo E-mail (emailaddress1) está em um formato inválido.");
            }
        }

        private static bool IsValidEmail(string email)
        {
            try
            {
                var address = new MailAddress(email);
                return string.Equals(address.Address, email, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static void ValidateTelephone(Entity target, string attributeName, ITracingService tracing)
        {
            if (!target.Contains(attributeName))
            {
                return;
            }

            var telephone = target.GetAttributeValue<string>(attributeName);

            // Não é obrigatório: se estiver vazio/nulo, não valida formato.
            if (string.IsNullOrWhiteSpace(telephone))
            {
                return;
            }

            telephone = telephone.Trim();

            if (!TelephoneRegex.IsMatch(telephone))
            {
                tracing?.Trace("Validação falhou: telefone inválido. Atributo: {0}. Valor: '{1}'", attributeName, telephone);
                throw new InvalidPluginExecutionException("O campo Telefone (telephone1) deve conter apenas 10 ou 11 dígitos.");
            }
        }
    }
}
