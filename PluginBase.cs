using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Extensions;
using Microsoft.Xrm.Sdk.PluginTelemetry;
using System;
using System.Runtime.CompilerServices;
using System.ServiceModel;

namespace Dynamics_Projeto
{
    /// <summary>
    /// Classe base para todas as classes de plug-in do Dataverse/Dynamics 365.
    /// </summary>
    /// <remarks>
    /// <para>
    /// No Dataverse, um “plug-in” é código .NET executado no servidor durante o processamento de uma mensagem
    /// (por exemplo: Create/Update/Delete) em etapas do pipeline (Pré-Validação, Pré-Operação e Pós-Operação).
    /// </para>
    /// <para>
    /// Esta classe centraliza o código “padrão” (boilerplate): obtém o contexto de execução, serviços e rastreamento.
    /// Para criar um plugin de verdade, você normalmente herda desta classe e sobrescreve
    /// <see cref="ExecuteDataversePlugin(ILocalPluginContext)"/>.
    /// </para>
    /// <para>
    /// Guia de desenvolvimento de plugins: https://docs.microsoft.com/powerapps/developer/common-data-service/plug-ins
    /// Melhores práticas e orientações: https://docs.microsoft.com/powerapps/developer/common-data-service/best-practices/business-logic/
    /// </para>
    /// </remarks>
    public abstract class PluginBase : IPlugin
    {
        /// <summary>
        /// Nome da classe do plugin (usado apenas para rastreamento/diagnóstico).
        /// </summary>
        protected string PluginClassName { get; }

        /// <summary>
        /// Inicializa uma nova instância da classe <see cref="PluginBase"/>.
        /// </summary>
        /// <param name="pluginClassName">O <see cref="Type"/> da classe do plugin.</param>
        internal PluginBase(Type pluginClassName)
        {
            PluginClassName = pluginClassName.ToString();
        }

        /// <summary>
        /// Ponto de entrada principal para a lógica de negócio que o plug-in deve executar.
        /// </summary>
        /// <param name="serviceProvider">O provedor de serviços.</param>
        /// <remarks>
        /// O Dataverse chama este método automaticamente quando o step do plugin é executado.
        /// Você normalmente não chama este método manualmente.
        /// </remarks>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters", Justification = "Execute")]
        public void Execute(IServiceProvider serviceProvider)
        {
            if (serviceProvider == null)
            {
                throw new InvalidPluginExecutionException(nameof(serviceProvider));
            }

            // Constrói o contexto local do plug-in.
            // Este objeto encapsula serviços comuns (contexto, tracing, OrganizationService etc.).
            var localPluginContext = new LocalPluginContext(serviceProvider);

            // Trace é o log do lado do servidor (útil para depurar problemas em produção).
            // CorrelationId ajuda a correlacionar logs entre múltiplas execuções/serviços no mesmo “fluxo” do Dataverse.
            localPluginContext.Trace($"Entered {PluginClassName}.Execute() " +
                $"Correlation Id: {localPluginContext.PluginExecutionContext.CorrelationId}, " +
                $"Initiating User: {localPluginContext.PluginExecutionContext.InitiatingUserId}");

            try
            {
                // Invoca a implementação personalizada
                ExecuteDataversePlugin(localPluginContext);

                // Agora sai - se o plugin derivado registrou incorretamente eventos sobrepostos, evita múltiplas execuções.
                return;
            }
            catch (FaultException<OrganizationServiceFault> orgServiceFault)
            {
                // Erros vindos do Dataverse/OrganizationService chegam aqui encapsulados.
                // Re-lançamos como InvalidPluginExecutionException para o Dataverse tratar corretamente (e reverter a transação, quando aplicável).
                localPluginContext.Trace($"Exceção: {orgServiceFault.ToString()}");

                throw new InvalidPluginExecutionException($"OrganizationServiceFault: {orgServiceFault.Message}", orgServiceFault);
            }
            finally
            {
                localPluginContext.Trace($"Saindo de {PluginClassName}.Execute()");
            }
        }

        /// <summary>
        /// Espaço reservado para uma implementação personalizada do plug-in.
        /// </summary>
        /// <param name="localPluginContext">Contexto para o plug-in atual.</param>
        /// <remarks>
        /// Dicas para iniciantes:
        /// - Use <see cref="ILocalPluginContext.PluginExecutionContext"/> para descobrir mensagem, entidade, estágio e imagens.
        /// - Use <see cref="ILocalPluginContext.InitiatingUserService"/> para executar operações respeitando o usuário que iniciou a ação.
        /// - Use <see cref="ILocalPluginContext.PluginUserService"/> para executar operações como o usuário configurado no step do plugin.
        /// - Use <see cref="ILocalPluginContext.Trace(string, string)"/> para registrar diagnóstico.
        /// - Para bloquear a operação e mostrar uma mensagem, lance <see cref="InvalidPluginExecutionException"/>.
        /// </remarks>
        protected virtual void ExecuteDataversePlugin(ILocalPluginContext localPluginContext)
        {
            // Implementações derivadas sobrescrevem este método.
            // A classe base é intencionalmente vazia.
        }
    }

    /// <summary>
    /// Abstração sobre o <see cref="IServiceProvider"/> com os serviços mais usados no desenvolvimento de plugins do Dataverse.
    /// </summary>
    /// <remarks>
    /// Centraliza o acesso aos serviços do Dataverse para evitar chamadas repetidas ao <see cref="IServiceProvider"/>
    /// e para manter o código do seu plugin mais simples.
    /// </remarks>
    public interface ILocalPluginContext
    {
        /// <summary>
        /// Serviço da organização Dataverse para o usuário que iniciou a ação.
        /// Em geral, é o “usuário real” que executou o comando no Dynamics (quem clicou em Salvar, por exemplo).
        /// </summary>
        IOrganizationService InitiatingUserService { get; }

        /// <summary>
        /// Serviço da organização Dataverse para a conta sob a qual o plugin está executando.
        /// Normalmente é o usuário configurado no registro do step do plugin (pode ser igual ao InitiatingUserService).
        /// </summary>
        IOrganizationService PluginUserService { get; }

        /// <summary>
        /// IPluginExecutionContext contém informações que descrevem o ambiente de execução do plug-in, informações relacionadas ao pipeline de execução e informações de negócio da entidade.
        /// </summary>
        IPluginExecutionContext PluginExecutionContext { get; }

        /// <summary>
        /// Plug-ins registrados como síncronos podem postar o contexto de execução no Microsoft Azure Service Bus.<br/>
        /// É por meio deste serviço de notificação que plug-ins síncronos podem enviar mensagens para o Microsoft Azure Service Bus.
        /// </summary>
        IServiceEndpointNotificationService NotificationService { get; }

        /// <summary>
        /// Fornece informações de rastreamento de execução para plug-ins.
        /// </summary>
        ITracingService TracingService { get; }

        /// <summary>
        /// Provedor de serviço geral para recursos não contemplados na classe base.
        /// </summary>
        IServiceProvider ServiceProvider { get; }

        /// <summary>
        /// Fábrica de OrganizationService para criar conexões para outros usuários ou sistema.
        /// </summary>
        IOrganizationServiceFactory OrgSvcFactory { get; }

        /// <summary>
        /// ILogger para este plugin.
        /// </summary>
        ILogger Logger { get;  }

        /// <summary>
        /// Escreve uma mensagem de rastreamento no log.
        /// </summary>
        /// <param name="message">Mensagem a ser rastreada.</param>
        /// <param name="method">Nome do método chamador (preenchido automaticamente pelo compilador).</param>
        void Trace(string message, [CallerMemberName] string method = null);
    }

    /// <summary>
    /// Implementação padrão de <see cref="ILocalPluginContext"/>.
    /// </summary>
    /// <remarks>
    /// Resolve e mantém em cache os serviços do <see cref="IServiceProvider"/> para reutilização durante a execução do plugin.
    /// </remarks>
    public class LocalPluginContext : ILocalPluginContext
    {
        /// <summary>
        /// Serviço da organização Dataverse para o usuário que iniciou a ação.
        /// É o usuário “real” que disparou o plugin (por exemplo, ao criar/atualizar um registro na tela).
        /// </summary>
        public IOrganizationService InitiatingUserService { get; }

        /// <summary>
        /// Serviço da organização Dataverse para a conta sob a qual o plugin está executando.
        /// Normalmente é o usuário configurado no step do plugin (pode ser igual ao InitiatingUserService).
        /// </summary>
        public IOrganizationService PluginUserService { get; }

        /// <summary>
        /// IPluginExecutionContext contém informações que descrevem o ambiente de execução do plug-in, informações relacionadas ao pipeline de execução e informações de negócio da entidade.
        /// </summary>
        public IPluginExecutionContext PluginExecutionContext { get; }

        /// <summary>
        /// Plug-ins registrados como síncronos podem postar o contexto de execução no Microsoft Azure Service Bus.<br/>
        /// É por meio deste serviço de notificação que plug-ins síncronos podem enviar mensagens para o Microsoft Azure Service Bus.
        /// </summary>
        public IServiceEndpointNotificationService NotificationService { get; }

        /// <summary>
        /// Fornece informações de rastreamento de execução para plug-ins.
        /// </summary>
        public ITracingService TracingService { get; }

        /// <summary>
        /// Provedor de serviço geral para recursos não contemplados na classe base.
        /// </summary>
        public IServiceProvider ServiceProvider { get; }

        /// <summary>
        /// Fábrica de OrganizationService para criar conexões para outros usuários ou sistema.
        /// </summary>
        public IOrganizationServiceFactory OrgSvcFactory { get; }

        /// <summary>
        /// ILogger para este plugin.
        /// </summary>
        public ILogger Logger { get; }

        /// <summary>
        /// Objeto auxiliar que armazena os serviços disponíveis neste plug-in.
        /// </summary>
        /// <param name="serviceProvider">Service provider fornecido pelo Dataverse quando o plugin é executado.</param>
        public LocalPluginContext(IServiceProvider serviceProvider)
        {
            if (serviceProvider == null)
            {
                throw new InvalidPluginExecutionException(nameof(serviceProvider));
            }

            ServiceProvider = serviceProvider;

            // ILogger (PluginTelemetry) pode ser usado para logs estruturados/telemetria dependendo da sua configuração.
            Logger = serviceProvider.Get<ILogger>();

            // Contexto do pipeline: contém MessageName, Stage, PrimaryEntityName, InputParameters, Pre/Post Images, etc.
            PluginExecutionContext = serviceProvider.Get<IPluginExecutionContext>();

            // ITracingService é o “trace log” tradicional do Dataverse; aqui embrulhamos para prefixar com delta de tempo.
            TracingService = new LocalTracingService(serviceProvider);

            NotificationService = serviceProvider.Get<IServiceEndpointNotificationService>();

            OrgSvcFactory = serviceProvider.Get<IOrganizationServiceFactory>();

            // Usuário configurado para execução do plugin (registrado no step). Pode ser o mesmo do usuário que iniciou.
            PluginUserService = serviceProvider.GetOrganizationService(PluginExecutionContext.UserId);

            // Usuário que iniciou a ação (quem de fato disparou o evento).
            InitiatingUserService = serviceProvider.GetOrganizationService(PluginExecutionContext.InitiatingUserId);

        }

        /// <summary>
        /// Escreve uma mensagem de rastreamento no log.
        /// </summary>
        /// <param name="message">Mensagem a ser rastreada.</param>
        /// <param name="method">Nome do método chamador (preenchido automaticamente pelo compilador).</param>
        public void Trace(string message, [CallerMemberName] string method = null)
        {
            if (string.IsNullOrWhiteSpace(message) || TracingService == null)
            {
                return;
            }

            if (method != null)
                TracingService.Trace($"[{method}] - {message}");
            else
                TracingService.Trace($"{message}");
        }
    }

    /// <summary>
    /// Implementação especializada de ITracingService que prefixa todas as mensagens rastreadas com um delta de tempo para diagnóstico de performance do Plugin
    /// </summary>
    public class LocalTracingService : ITracingService
    {
        private readonly ITracingService _tracingService;

        private DateTime _previousTraceTime;

        public LocalTracingService(IServiceProvider serviceProvider)
        {
            DateTime utcNow = DateTime.UtcNow;

            var context = (IExecutionContext)serviceProvider.GetService(typeof(IExecutionContext));

            DateTime initialTimestamp = context.OperationCreatedOn;

            if (initialTimestamp > utcNow)
            {
                initialTimestamp = utcNow;
            }

            _tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            _previousTraceTime = initialTimestamp;
        }

        public void Trace(string message, params object[] args)
        {
            var utcNow = DateTime.UtcNow;

            // Duração desde o último Trace (útil para entender “onde está gastando tempo” dentro do plugin).
            var deltaMilliseconds = utcNow.Subtract(_previousTraceTime).TotalMilliseconds;

            try
            {

                if (args == null || args.Length == 0)
                    _tracingService.Trace($"[+{deltaMilliseconds:N0}ms] - {message}");
                else
                    _tracingService.Trace($"[+{deltaMilliseconds:N0}ms] - {string.Format(message, args)}");
            }
            catch (FormatException ex)
            {
                throw new InvalidPluginExecutionException($"Failed to write trace message due to error {ex.Message}", ex);
            }
            _previousTraceTime = utcNow;
        }
    }
}
