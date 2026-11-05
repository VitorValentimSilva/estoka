# Progresso do Estoka

Legenda: ✅ feito · 🚧 em andamento · ⏳ pendente

## Fase 1 — Fundação ✅
- ✅ Solução `Estoka.sln`, camadas Domain / Application / Infrastructure / Web, projetos de teste
- ✅ `Directory.Build.props` (nullable, TreatWarningsAsErrors em `src/`), `Directory.Packages.props` (CPM), `.editorconfig`, `global.json`
- ✅ `dotnet-ef` como ferramenta local (`dotnet-tools.json`)
- ✅ Domínio: Category, Supplier, Product, Customer (PF/PJ + CPF/CNPJ), StockMovement, SalesOrder/Item, AuditLog
- ✅ Regras: estoque nunca negativo, máquina de estados do pedido, snapshot de preço/custo, totais
- ✅ EF Core 10 + Npgsql: configurações Fluent API, snake_case, índices (trigram para busca), check constraints
- ✅ Concorrência otimista via `xmin` (Product e SalesOrder) + retry na confirmação de pedido
- ✅ Interceptor de auditoria (CreatedAt/By, UpdatedAt/By, soft delete, AuditLog com antes/depois em jsonb)
- ✅ Migration inicial versionada; aplicada no startup
- ✅ Seed determinístico: 8 categorias, 10 fornecedores, 120 produtos, 60 clientes, ~260 pedidos em 90 dias, movimentações coerentes, produtos abaixo do mínimo
- ✅ Testes unitários (domínio, CPF/CNPJ, connection string, agenda do reset, permissões)
- ✅ Testes de integração com Postgres real (seed, consistência estoque × movimentações, concorrência do último item, fluxo do pedido, autorização no serviço, auditoria)

## Fase 2 — Autenticação + layout + cadastros ✅
- ✅ ASP.NET Core Identity com cookie (`estoka.auth`), sem cadastro público; login SSR com antiforgery e bloqueio de usuário desativado
- ✅ Caixa "Acesso de demonstração" (clique preenche e-mail/senha)
- ✅ Policies por permissão derivadas da mesma matriz usada nos serviços; `AuthorizeRouteView` + `AuthorizeView` no menu
- ✅ Usuário corrente propagado ao circuito do Blazor Server (CircuitHandler) e às requisições HTTP (middleware)
- ✅ Revalidação periódica da sessão (usuário desativado/papel alterado derruba o circuito)
- ✅ Data Protection keys no banco (`data_protection_keys`)
- ✅ Layout MudBlazor 9: AppBar, Drawer responsivo, tema claro/escuro salvo em cookie, breadcrumbs, avatar com menu, faixa de demo, rodapé configurável (`Branding`)
- ✅ Produtos: MudDataGrid com paginação/ordenação/filtro no servidor, busca nome/SKU, filtro por categoria e "abaixo do mínimo", diálogo com FluentValidation, histórico de movimentações, margem oculta para Vendedor
- ✅ Categorias, Fornecedores, Clientes: CRUD com validação (CPF/CNPJ, e-mail, telefone/CEP com máscara), confirmação de exclusão, snackbars
- ✅ `/health` com verificação do banco; `PORT`, forwarded headers
- ✅ Testes de integração HTTP: redirect para login, login dos 3 perfis, senha errada, usuário desativado, health
## Fase 3 — Estoque, pedidos e dashboard ✅
- ✅ Estoque: lista de movimentações (filtros por tipo, período, texto e produto), registrar entrada e ajuste de inventário (pela quantidade contada)
- ✅ Atalho "Entrada" nos alertas do dashboard abre o diálogo já com o produto e a quantidade sugerida
- ✅ Pedidos: lista com filtros por status, período e cliente; clique abre o pedido
- ✅ Tela do pedido: autocomplete de cliente e produto, itens editáveis, totais em tempo real, aviso de estoque insuficiente, botões conforme o fluxo (confirmar, faturar, entregar, cancelar com motivo) e linha do tempo
- ✅ Vendedor não altera preço de tabela (só concede desconto) — regra também no serviço
- ✅ PDF do pedido (QuestPDF, licença Community) em `/pedidos/{id}/pdf`
- ✅ Dashboard: faturamento, pedidos, ticket médio (com variação vs. mesmo período do mês anterior), itens abaixo do mínimo, gráfico de 30 dias, vendas por categoria, top 5, alertas e pedidos recentes
- ✅ ProblemDetails para erros em downloads/API
- ✅ E2E com Playwright (`tests/e2e/pedido-vendedor.cjs`): Vendedor cria e confirma pedido e o estoque baixa
- ✅ Testes de integração: dashboard consistente com os pedidos, PDF autenticado, 404 em ProblemDetails
## Fase 4 — Relatórios, auditoria, API, Docker, prints e documentação ✅
- ✅ Relatórios: vendas por período (diário + forma de pagamento + margem), curva ABC, posição de estoque — Excel (ClosedXML) e PDF (QuestPDF)
- ✅ Usuários (Admin): listar, criar, mudar papel, ativar/desativar (security stamp derruba sessões), com auditoria
- ✅ Auditoria (Admin/Gerente): filtros por entidade, ação, usuário e período; diff antes/depois
- ✅ API REST `/api/v1` com API key (`X-Api-Key`), ProblemDetails, OpenAPI nativo (`/openapi/v1.json`) e Scalar (`/scalar`)
- ✅ Dockerfile multi-stage (sdk → aspnet, usuário não-root), `.dockerignore`, `docker-compose.yml`, `render.yaml`
- ✅ Imagem testada: `docker build` + container contra o Postgres local (PORT, URI de conexão, forwarded headers, WebSockets, PDF/Excel, E2E)
- ✅ Prints em `docs/screenshots` (1440px e 390px, claro e escuro) revisados e ajustados
- ✅ README (PT + seção em inglês), CLAUDE.md

## Verificação final
- ✅ `dotnet build` sem warnings · `dotnet test` 88 testes (45 unitários + 43 integração) passando
- ✅ E2E Playwright do Vendedor (local e no container)

## Falta / próximos passos (v2)
- NF-e (emissão via provedor), multiempresa, leitor de código de barras, app mobile/PWA, CI no GitHub Actions
