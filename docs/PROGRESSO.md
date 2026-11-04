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

## Fase 2 — Autenticação + layout + cadastros ⏳
## Fase 3 — Estoque, pedidos e dashboard ⏳
## Fase 4 — Relatórios, auditoria, API, Docker, prints e documentação ⏳
