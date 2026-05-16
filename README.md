# Dynamics_Projeto

Projeto de plugins para Microsoft Dataverse / Dynamics 365, com foco em validacoes de dados e bloqueio de duplicidade de oportunidades para veiculos.

## O que este projeto faz

Este repositório contem dois plugins principais:

- **ContactPreOperationValidationPlugin**: valida campos do contato antes da gravação.
- **OpportunityBloqueioDuplicidadeVeiculoPlugin**: impede duplicidade de oportunidades abertas para o mesmo veiculo.

## Tecnologias usadas

- C#
- .NET Framework 4.6.2
- Microsoft.PowerApps.MSBuild.Plugin
- Microsoft.CrmSdk.CoreAssemblies
- Assinatura com arquivo `.snk` mantido apenas localmente

## Estrutura do projeto

- `Dynamics_Projeto.csproj`: arquivo principal do projeto.
- `Dynamics Projeto.sln`: solucao do Visual Studio.
- `PluginBase.cs`: base compartilhada do plugin.
- `Validação de Dados.cs`: validacoes de campos do contato.
- `Bloqueio de duplicatas Veiculos.cs`: bloqueio de duplicidade por veiculo.

## Como compilar

1. Abra um terminal na pasta do projeto.
2. Execute:

```powershell
dotnet build Dynamics_Projeto.csproj
```

Se o build terminar sem erros, o projeto esta pronto para ser versionado.

## Como implantar no Dataverse

Depois de compilar, o caminho mais comum para publicar o plugin e:

1. Gerar a DLL a partir do projeto.
2. Registrar a DLL no ambiente do Dataverse com uma ferramenta como o Plugin Registration Tool.
3. Criar ou atualizar o step do plugin na entidade correta.
4. Conferir o modo de execucao, a etapa do pipeline e os atributos utilizados na logica.

### Observacoes importantes

- O plugin de duplicidade de veiculos roda em `opportunity` no estágio `PreOperation`.
- O plugin de validacao de contato roda em `contact` no estágio `PreOperation` e em modo sincronico.
- Se o repositório for publico, mantenha a chave `.snk` apenas localmente.

## Como publicar no GitHub

### 1. Criar o repositório local

Se a pasta ainda nao for um repositório Git, execute:

```powershell
git init
git add .
git commit -m "Initial commit"
```

### 2. Criar o repositório no GitHub

No VS Code, com a extensao GitHub instalada, voce pode:

- abrir o painel **Source Control**
- escolher **Publish to GitHub**
- informar nome e visibilidade do repositorio

Ou criar manualmente no site do GitHub e depois conectar o remoto.

### 3. Conectar o remoto

Depois de criar o repo no GitHub, adicione o remoto:

```powershell
git remote add origin https://github.com/SEU_USUARIO/Dynamics_Projeto.git
git branch -M main
git push -u origin main
```

## Dicas para o primeiro push

- Verifique se o `.gitignore` esta ignorando `bin/` e `obj/`.
- Confirme se a sua conta GitHub esta conectada ao VS Code.
- Se o push pedir autenticacao, use o navegador ou o fluxo da extensao GitHub.

## Observacoes

Este projeto usa plugins do Dataverse, entao o deploy final normalmente e feito registrando a DLL no ambiente correto, nao apenas enviando o codigo-fonte para o GitHub.

Se o repositório for publico, mantenha a chave `.snk` fora do Git e gere uma nova chave privada apenas no ambiente local.