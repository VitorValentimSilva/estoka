# CLAUDE.md — guia para futuras sessões

Estoka: painel de estoque e vendas (portfólio), .NET 10 + Blazor Interactive Server + MudBlazor 9 + EF Core 10/PostgreSQL.
Demo para a empresa fictícia "Distribuidora Bom Preço" (Campinas – SP). Interface e mensagens em **pt-BR**.

## Comandos

```bash
pg_ctlcluster 16 main start            # Postgres local (usuário/senha/banco: estoka/estoka/estoka)
dotnet tool restore                    # dotnet-ef local (dotnet-tools.json)
dotnet build                           # deve sair com 0 warnings (TreatWarningsAsErrors em src/)
dotnet test                            # unit + integração (Microsoft.Testing.Platform via global.json)
dotnet test --project tests/Estoka.IntegrationTests --filter-method "*Concorr*"
dotnet run --project src/Estoka.Web    # http://localhost:5016 (Development)
dotnet ef migrations add Nome -p src/Estoka.Infrastructure -s src/Estoka.Web -o Persistence/Migrations
cd tests/e2e && npm ci                                                   # Playwright do E2E (CI instala o chromium)
BASE_URL=http://localhost:8080 node tests/e2e/pedido-vendedor.cjs      # E2E
dotnet test --coverage --coverage-output-format cobertura --coverage-output coverage.cobertura.xml
BASE_URL=... ORDER_ID=<guid> node tests/e2e/screenshots.cjs            # regenera docs/screenshots
```

## Arquitetura (Clean Architecture enxuta)

- `Estoka.Domain` — entidades com comportamento; sem dependências. Regras: `Product.RegisterMovement` (único ponto que muda estoque; nunca negativo; quantidade da movimentação **com sinal**), `SalesOrder` (máquina de estados Rascunho → Confirmado → Faturado → Entregue | Cancelado; totais).
- `Estoka.Application` — serviços de caso de uso (um por agregado), DTOs (records), inputs (classes mutáveis p/ MudForm) + validadores FluentValidation, `RolePermissions` (matriz papel → permissão), portas (`IAppDbContext`, `IAppDbContextFactory`, `ICurrentUser`, `IReportExporter`, `IOrderDocumentRenderer`, `IUserAdminService`).
- `Estoka.Infrastructure` — `AppDbContext` (Identity + DataProtection), `IEntityTypeConfiguration`s, migrations, `AuditSaveChangesInterceptor`, seed/reset da demo, Identity (sign-in manager que bloqueia inativos), exportadores QuestPDF/ClosedXML, `ConnectionStringHelper` (aceita URI).
- `Estoka.Web` — Blazor (Components/Pages/*), API mínima (`Api/`), endpoints de download (`Endpoints/`), autenticação/policies (`Infrastructure/`).

## Convenções e regras

- **Sem repositório genérico.** Serviços usam `IAppDbContextFactory` → **um DbContext por operação** (`await using var db = await dbFactory.CreateAsync(ct)`), nunca um contexto de vida longa no circuito.
- **Autorização no serviço** sempre: primeira linha `user.Demand(Permission.X)`. Custo/margem só com `Permission.ViewCost` (DTOs trazem `null` para quem não pode). A UI esconde, mas o serviço é quem garante.
- Exceções de negócio: `DomainException` (mensagem amigável pt-BR), `NotFoundException`, `ForbiddenException`, `ConcurrencyConflictException`, `ValidationException` (FluentValidation). A UI usa `Snackbar.RunAsync(...)`; a API converte em ProblemDetails (`Endpoints/ExceptionMapping.cs`).
- Toda alteração de estoque gera `StockMovement`; operações que mexem em vários agregados ficam numa transação (`db.Database.BeginTransactionAsync`). Confirmação/cancelamento de pedido usam retry de concorrência (`xmin`).
- Soft delete (`ISoftDeletable`) com query filter em cadastros. Para histórico (pedidos/movimentações de registros excluídos) use `IgnoreQueryFilters()`.
- Datas: `DateTimeOffset` UTC no banco; converter com `BrazilTime` (America/Sao_Paulo). Dinheiro `decimal(18,2)`, quantidades `(18,3)`; formatar com `UiExtensions.Money()/Qty()`.
- Banco em snake_case (EFCore.NamingConventions). Índices trigram (`gin_trgm_ops`) para buscas `ILIKE`.
- Pacotes: versões só em `Directory.Packages.props` (CPM).
- Regras comerciais simples ficam em `SalesPolicy` (ex.: desconto máx. 10% sem `GrantUnlimitedDiscount`).
- Telemetria: use `EstokaTelemetry.Source`/contadores (System.Diagnostics) na Application; o OpenTelemetry é configurado só na Web (`TelemetrySetup`).
- Segurança na Web: `SecuritySetup` (rate limiting por IP em `/login` POST e `/api`, CSP e demais cabeçalhos). Ao adicionar CDN/scripts externos, atualize a CSP.
- Demo: contas de `DemoUsers` são protegidas no `UserAdminService`; o reset (`DemoResetBackgroundService`) roda no horário ou ao iniciar se estiver atrasado (`system_settings`).
- Commits pequenos, mensagens em português. Atualize `docs/PROGRESSO.md`.

## Pegadinhas conhecidas

- Razor: `switch { < 12 => ... }` dentro de `.razor` é lido como tag — use if/ternário.
- MudBlazor 9: `MudChart<T>` genérico com `ChartSeries<T>`/`ChartData<T>`; `MudTabs` não tem `PanelClass` (usar `TabPanelsClass`); `MudText` usa `HtmlTag`.
- `FluentValidation.Severity` × `MudBlazor.Severity`: em arquivos com os dois, use `@using FV = FluentValidation`.
- .NET 10: no `IExceptionHandler` o `Request.Path` já é o do handler (`/erro`); use `IExceptionHandlerPathFeature.Path`.
- Docker: o `publish` precisa fazer restore com os `.razor` presentes (senão `blazor.web.js` não entra).
- Não rode `pkill -f Estoka.Web` (casa com o próprio shell); mate pelo PID.
- `IdentitySeeder.EnsureDemoUsersAsync` só redefine senha se necessário — trocar o security stamp derruba sessões.
- Testes de integração fazem muitos logins: a `EstokaFactory` sobe os limites de rate limiting; o 429 é testado em `LowRateLimitFactory` (banco próprio).
- Rodar em `Production` a partir de `bin/Debug` não serve os assets estáticos fingerprinted — use `Development` ou a imagem publicada.

## Testes

- Integração: `EstokaFactory` cria um banco `estoka_test_<guid>` no Postgres (`ESTOKA_TEST_PG` para outro servidor), roda migrations + seed e apaga no fim. `factory.ScopeAs(Roles.X)` dá um escopo de DI autenticado; `client.LoginAsync(email)` faz login real pelo formulário.
- Crie dados próprios nos testes (`CreateProductAsync`, `CreateCustomerAsync`) em vez de depender de itens específicos do seed.
