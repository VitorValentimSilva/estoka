# Estoka — gestão de estoque e vendas para pequenas empresas

![.NET 10](https://img.shields.io/badge/.NET-10-512BD4) ![Blazor](https://img.shields.io/badge/Blazor-Interactive%20Server-5C2D91) ![EF Core](https://img.shields.io/badge/EF%20Core-10-0F766E) ![PostgreSQL](https://img.shields.io/badge/PostgreSQL-16-336791) ![Tests](https://img.shields.io/badge/testes-88%20%2B%20E2E-16A34A)

**Demo:** (link) · usuários `admin@estoka.app`, `gerente@estoka.app`, `vendedor@estoka.app` — senha `Demo@1234`
**API:** (link)/scalar · chave de demonstração `estoka-demo-key`

---

## 🇺🇸 In English (for clients)

**Estoka** is a production-style inventory & sales back office for small businesses, built by me as a portfolio project. The demo is configured for a *fictional* wholesale distributor in Campinas, Brazil.

- **Stack:** .NET 10, Blazor Web App (Interactive Server) + MudBlazor, EF Core 10 + PostgreSQL, ASP.NET Core Identity, Minimal APIs with native OpenAPI + Scalar, QuestPDF, ClosedXML, Docker.
- **Architecture:** lean Clean Architecture (Domain / Application / Infrastructure / Web), business rules in the domain, authorization enforced in the services (not only in the UI).
- **Highlights:** stock can never go negative; confirming an order writes stock movements in one transaction; **optimistic concurrency** with PostgreSQL `xmin` (two users racing for the last unit → only one wins, with a friendly message); automatic **audit trail** (before/after JSON) via an EF Core interceptor; soft delete with global query filters; role-based access (Admin / Manager / Salesperson — salespeople never see cost or margin); daily-reset demo data.
- **Quality:** 88 unit + integration tests against a real PostgreSQL (WebApplicationFactory), plus a Playwright end-to-end flow. Ships with a non-root multi-stage Dockerfile, docker-compose and a Render blueprint.

I build this kind of system for clients — internal tools, dashboards, APIs and integrations in .NET.

---

## O que é

O **Estoka** é um painel administrativo de **estoque e vendas** para pequenas empresas: cadastro de produtos, clientes e fornecedores, controle de estoque por movimentações, pedidos de venda com baixa automática, dashboard, relatórios com exportação e auditoria completa.

A demonstração vem configurada para a empresa fictícia **Distribuidora Bom Preço (Campinas – SP)**, com ~120 produtos, 60 clientes (CPF/CNPJ válidos gerados) e ~260 pedidos dos últimos 90 dias. Os dados são recriados todos os dias às 4h (horário de Brasília).

> Projeto fictício desenvolvido para portfólio por **Vitor Valentim Silva**.

## Prints

| Dashboard | Dashboard (tema escuro) |
|---|---|
| ![Dashboard](docs/screenshots/dashboard-desktop.png) | ![Dashboard escuro](docs/screenshots/dashboard-desktop-escuro.png) |

| Produtos | Pedido |
|---|---|
| ![Produtos](docs/screenshots/produtos-desktop.png) | ![Pedido](docs/screenshots/pedido-desktop.png) |

| Curva ABC | Auditoria (antes/depois) |
|---|---|
| ![Curva ABC](docs/screenshots/relatorio-curva-abc-desktop.png) | ![Auditoria](docs/screenshots/auditoria-desktop.png) |

| Login (acesso de demonstração) | Estoque | API (Scalar) |
|---|---|---|
| ![Login](docs/screenshots/login-desktop.png) | ![Estoque](docs/screenshots/estoque-desktop.png) | ![API](docs/screenshots/api-scalar-desktop.png) |

**Celular (390px):**

| Login | Dashboard | Dashboard escuro | Produtos | Pedido | Curva ABC |
|---|---|---|---|---|---|
| ![](docs/screenshots/login-mobile.png) | ![](docs/screenshots/dashboard-mobile.png) | ![](docs/screenshots/dashboard-mobile-escuro.png) | ![](docs/screenshots/produtos-mobile.png) | ![](docs/screenshots/pedido-mobile.png) | ![](docs/screenshots/relatorio-curva-abc-mobile.png) |

## Funcionalidades por papel

| Funcionalidade | Admin | Gerente | Vendedor |
|---|:-:|:-:|:-:|
| Dashboard (faturamento, pedidos, ticket médio, gráficos, alertas) | ✅ | ✅ | ✅ |
| Produtos — consultar preço e estoque | ✅ | ✅ | ✅ |
| Produtos — **ver custo e margem** | ✅ | ✅ | ❌ |
| Produtos, categorias e fornecedores — cadastrar/editar/excluir | ✅ | ✅ | ❌ |
| Clientes — CRUD (CPF/CNPJ validados, máscaras) | ✅ | ✅ | ✅ |
| Pedidos — criar, confirmar, faturar, entregar, cancelar, PDF | ✅ | ✅ | ✅ (sem alterar preço de tabela) |
| Estoque — consultar movimentações | ✅ | ✅ | ✅ |
| Estoque — registrar entrada e ajuste de inventário | ✅ | ✅ | ❌ |
| Relatórios (vendas, curva ABC, posição de estoque) + Excel/PDF | ✅ | ✅ | ❌ |
| Auditoria (quem alterou o quê, antes/depois) | ✅ | ✅ | ❌ |
| Usuários (criar, mudar papel, desativar) | ✅ | ❌ | ❌ |

As permissões ficam numa matriz única (`RolePermissions`) usada **tanto** pelas policies da interface/rotas **quanto** pelos serviços — um Vendedor que chame o serviço diretamente recebe `ForbiddenException`, e o DTO de produto chega sem custo/margem.

## Arquitetura

```mermaid
flowchart TB
    subgraph Web["Estoka.Web — Blazor Interactive Server + Minimal APIs"]
        UI["Páginas MudBlazor<br/>(AuthorizeView, policies)"]
        API["/api/v1 (API key)<br/>OpenAPI + Scalar"]
        DOCS["Downloads PDF/Excel"]
    end
    subgraph App["Estoka.Application — casos de uso"]
        SVC["Serviços (ProductService, SalesOrderService...)<br/>DTOs · FluentValidation · autorização"]
        PORTS["Portas: IAppDbContext, ICurrentUser,<br/>IReportExporter, IUserAdminService"]
    end
    subgraph Dom["Estoka.Domain — regras de negócio"]
        ENT["Product · StockMovement · SalesOrder<br/>Customer · Category · Supplier · AuditLog"]
    end
    subgraph Infra["Estoka.Infrastructure"]
        EF["AppDbContext (EF Core + Npgsql)<br/>configurações · migrations · interceptor de auditoria"]
        ID["ASP.NET Core Identity"]
        EXP["QuestPDF · ClosedXML"]
        SEED["Seed demo + reset diário"]
    end
    DB[(PostgreSQL)]

    UI --> SVC
    API --> SVC
    DOCS --> SVC
    SVC --> ENT
    SVC --> PORTS
    Infra -. implementa .-> PORTS
    EF --> DB
    ID --> DB
```

```
src/
  Estoka.Domain/          entidades, enums, regras (estoque, fluxo do pedido), CPF/CNPJ
  Estoka.Application/     serviços de caso de uso, DTOs, validadores, permissões, portas
  Estoka.Infrastructure/  EF Core, migrations, interceptor, Identity, seed, PDF/Excel
  Estoka.Web/             Blazor (páginas/componentes), API mínima, autenticação, Program.cs
tests/
  Estoka.UnitTests/         domínio, validações, connection string, agenda do reset
  Estoka.IntegrationTests/  WebApplicationFactory + PostgreSQL real
  e2e/                      Playwright: fluxo do vendedor e geração dos prints
```

## Decisões técnicas

**Concorrência otimista com `xmin`.** `Product` e `SalesOrder` usam a coluna de sistema `xmin` do PostgreSQL como token de concorrência (`IsRowVersion()`), sem coluna extra. Confirmar um pedido roda numa transação: carrega pedido e produtos, gera as movimentações de **SAÍDA** e salva. Se outro pedido mexeu no mesmo produto no meio do caminho, o `UPDATE ... WHERE xmin = @old` não afeta linhas → `DbUpdateConcurrencyException` → o serviço refaz a operação do zero com dados frescos (até 3 tentativas). Se o estoque não for mais suficiente, a regra de domínio responde com *"Estoque insuficiente para..."*. O teste de integração dispara dois pedidos simultâneos pelo último item: exatamente um confirma. Há ainda um `CHECK (current_stock >= 0)` no banco como última linha de defesa.

**Estoque = soma das movimentações.** O saldo só muda via `Product.RegisterMovement(...)`, que valida e devolve o lançamento (com quantidade *com sinal* e saldo após). Assim o estoque é sempre auditável — e há teste garantindo que `current_stock == SUM(stock_movements.quantity)` para todos os produtos.

**Auditoria por interceptor do EF Core.** Um `SaveChangesInterceptor` preenche `CreatedAt/By` e `UpdatedAt/By`, converte exclusões em *soft delete* e grava `AuditLog` com os valores **antes/depois** (só campos alterados, em `jsonb`) — na mesma transação do `SaveChanges`. Nenhum serviço precisa lembrar de auditar; a tela de Auditoria mostra o diff.

**Sem repositório genérico.** O `DbContext` já é Unit of Work + Repository. Um `IRepository<T>` genérico em cima dele esconde recursos (projeções, `Include`, `ILike`, `ExecuteUpdate`), duplica abstrações e piora a performance. Os serviços usam `IAppDbContext` (interface com os `DbSet`s) — testável e explícito. A Application assume dependência do provider PostgreSQL (busca com `ILIKE` + índices trigram) por pragmatismo: o produto é PostgreSQL-first.

**Um `DbContext` por operação.** No Blazor Server o escopo de DI vive o circuito inteiro; um `DbContext` longo vira cache obsoleto e não suporta operações concorrentes. Os serviços pedem um contexto novo à `IAppDbContextFactory` a cada operação (fábrica *scoped*, para o interceptor enxergar o usuário logado).

**Interactive Server.** Para um painel interno, o Blazor Server entrega UI rica com C# de ponta a ponta, sem duplicar contratos numa API para o front, com o código e os dados sensíveis (custos!) só no servidor. Trade-offs: conexão SignalR por usuário e latência sensível — aceitáveis para o público de um back office. A tela de login é SSR estático (cookie + antiforgery), sem circuito.

**Usuário corrente no circuito.** `HttpContext` não existe durante o circuito; um `CircuitHandler` sincroniza o `ICurrentUser` com o `AuthenticationStateProvider` (que revalida a sessão a cada minuto — usuário desativado ou com papel alterado perde o acesso).

**Datas e dinheiro.** Datas em UTC (`timestamptz`) e exibidas em horário de Brasília; dinheiro em `numeric(18,2)`, quantidades em `numeric(18,3)`, formatados em pt-BR.

**Migrations no startup.** O app aplica migrations ao iniciar (`Database.MigrateAsync`) — prático para a demo e um único container. Num ambiente maior, isso vira um passo do pipeline de deploy (ex.: `dotnet ef migrations bundle`), com aprovação e rollback controlados.

## Como rodar

### Docker Compose (mais simples)

```bash
docker compose up --build
```

Acesse http://localhost:8080 e entre com um dos usuários de demonstração. A API fica em http://localhost:8080/scalar (chave `estoka-demo-key`).

### `dotnet run` (desenvolvimento)

Pré-requisitos: .NET 10 SDK e PostgreSQL 16 com um usuário/banco `estoka` (senha `estoka`), ou ajuste `ConnectionStrings:Default`.

```bash
dotnet tool restore
```

```bash
dotnet run --project src/Estoka.Web
```

As migrations e os dados de demonstração são aplicados na primeira execução.

Criar uma nova migration:

```bash
dotnet ef migrations add NomeDaMigration -p src/Estoka.Infrastructure -s src/Estoka.Web -o Persistence/Migrations
```

## Variáveis de ambiente

| Variável | Padrão | Descrição |
|---|---|---|
| `ConnectionStrings__Default` | `Host=localhost;Database=estoka;...` | Formato .NET **ou** URI (`postgresql://user:pass@host/db?sslmode=require`) |
| `Database__RequireSsl` | `true` em Production | Força `SSL Mode=Require` |
| `PORT` | — | Porta HTTP (o Render define); sem ela, 8080 no container |
| `Demo__Enabled` | `true` | Seed automático, usuários de demo, faixa de aviso e reset diário |
| `Demo__ResetHour` | `4` | Hora (Brasília) do reset diário da demo |
| `Demo__Password` | `Demo@1234` | Senha dos usuários de demonstração |
| `Api__Key` | `estoka-demo-key` | Chave aceita no cabeçalho `X-Api-Key` |
| `Branding__CompanyName` / `__CompanyCity` | Distribuidora Bom Preço / Campinas – SP | Empresa exibida no painel e nos PDFs |
| `Branding__AuthorName` / `__AuthorUrl` / `__RepositoryUrl` | — | Rodapé "Projeto fictício desenvolvido para portfólio por..." |

## API REST

Base `/api/v1`, autenticação por `X-Api-Key`, erros em **ProblemDetails** (RFC 9457), documentação OpenAPI nativa em `/openapi/v1.json` e UI do **Scalar** em `/scalar`.

| Método | Rota | Descrição |
|---|---|---|
| GET | `/products?search=&categoryId=&belowMinimum=&page=&pageSize=` | Produtos ativos (sem custo) |
| GET | `/products/{id}` | Detalhe do produto |
| GET | `/stock/movements?productId=&type=&from=&to=` | Movimentações de estoque |
| GET | `/stock/low` | Produtos abaixo do mínimo |
| GET | `/customers?search=` | Busca de clientes |
| GET | `/orders?status=&from=&to=&customerId=` | Pedidos |
| GET | `/orders/{id}` | Pedido com itens |
| POST | `/orders` | Cria pedido (rascunho); `"confirm": true` já confirma e baixa o estoque |
| POST | `/orders/{id}/confirm` · `/orders/{id}/cancel` | Fluxo do pedido |

```bash
curl -X POST http://localhost:8080/api/v1/orders -H "X-Api-Key: estoka-demo-key" -H "Content-Type: application/json" -d '{"customerId":"<id>","paymentMethod":"Pix","items":[{"productId":"<id>","quantity":2}],"confirm":true}'
```

## Testes

```bash
dotnet test
```

- **Unitários (45):** regras de estoque (nunca negativo, saldo = soma), fluxo de status e totais do pedido, CPF/CNPJ, conversão de connection string URI, agenda do reset, matriz de permissões.
- **Integração (43, PostgreSQL real):** cada execução cria um banco temporário, aplica migrations e o seed, e o remove no fim. Cobrem: consistência estoque × movimentações, **dois pedidos simultâneos pelo último item**, `xmin`, auditoria e soft delete, **Vendedor tentando ver custo** (serviço e HTTP), login real pelo formulário, usuário desativado, dashboard, PDF, exportações, API (401/400/404/422/201) e gestão de usuários. Use `ESTOKA_TEST_PG` para apontar outro servidor.
- **E2E (Playwright):** Vendedor faz login → cria pedido → confirma → confere a baixa de estoque.

```bash
npm i -D playwright
```

```bash
BASE_URL=http://localhost:8080 node tests/e2e/pedido-vendedor.cjs
```

## Deploy no Render + Neon

1. **Banco (Neon):** crie um projeto em [neon.tech](https://neon.tech) (região `sa-east-1` se disponível) e copie a *connection string* (`postgresql://...@...neon.tech/neondb?sslmode=require`). O formato URI é aceito diretamente.
2. **Render:** faça fork/push deste repositório no GitHub. No [Render](https://render.com), escolha **New → Blueprint** e selecione o repositório — o `render.yaml` cria o serviço Docker com health check em `/health`.
   - Alternativa manual: **New → Web Service → Docker**, health check path `/health`.
3. **Variáveis:** em *Environment*, preencha `ConnectionStrings__Default` com a string do Neon. `Api__Key` é gerada automaticamente (ou defina a sua). Mantenha `Demo__Enabled=true` para a demonstração.
4. **Deploy:** o primeiro boot aplica as migrations e gera os dados de demonstração (alguns segundos). Acesse a URL do Render e faça login com os usuários de demo.
5. **Observações:** o Render termina o HTTPS no proxy — o app usa `UseForwardedHeaders` e não força redirecionamento interno. WebSockets (SignalR do Blazor) funcionam nativamente. As chaves do Data Protection ficam no banco, então o login sobrevive a reinícios e novos deploys. No plano gratuito o serviço "dorme" após inatividade; o primeiro acesso demora alguns segundos.

## Limitações conhecidas

- Sem NF-e/integração fiscal; o PDF do pedido é um documento sem valor fiscal.
- Sem reserva de estoque para rascunhos (o estoque só é baixado na confirmação, como em muitos ERPs pequenos).
- Agregações do dashboard/relatórios são feitas parcialmente em memória — adequado para o volume de uma pequena empresa; para volumes grandes, usar views/materialized views.
- Usuários de demonstração e dados são públicos e reiniciados diariamente.
- Interface só em pt-BR.

---

Projeto fictício desenvolvido para portfólio por [Vitor Valentim Silva](https://github.com/VitorValentimSilva).
