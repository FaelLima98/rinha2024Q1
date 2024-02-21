using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using rinha_backend_24.Dominio.Entidades;
using rinha_backend_24.Modelos.Requisicao;

namespace rinha_backend_24.Endpoints
{
    public static class EndpointsExtension
    {
        public static WebApplication AddEndpoints(this WebApplication app)
        {
            app.MapPost("/clientes/{id}/transacoes", async (int id, TransacaoRequest request, [FromServices]IConfiguration config, CancellationToken cancellationToken) =>
            {
                if (!int.TryParse(request.Valor.ToString(), out var valor)) return Results.UnprocessableEntity("O valor tem que ser um inteiro.");
                if (!request.Tipo.Equals("c") && !request.Tipo.Equals("d")) return Results.UnprocessableEntity("O tipo está inválido.");
                if (string.IsNullOrEmpty(request.Descricao)) return Results.UnprocessableEntity("A descrição é obrigatória.");
                if (request.Descricao.Length > 10) return Results.UnprocessableEntity("A descrição tem mais que 10 caracteres.");

                using var semaphore = new SemaphoreSlim(1);
                await semaphore.WaitAsync(cancellationToken);

                try
                {
                    using var connection = new NpgsqlConnection(config.GetConnectionString("DB"));

                    await connection.OpenAsync(cancellationToken);

                    var query = $@"SELECT ""Id"",""Limite"", ""SaldoInicial""
                                FROM ""Clientes"" 
                                WHERE ""Id"" = {id};"
                    ;

                    var cliente = await connection.QueryFirstAsync<Cliente>(query);

                    if (cliente == null) return Results.NotFound("Não foi encontrado um cliente com o ID informado");

                    cliente.AdicionarNovaTransacao(valor, request.Tipo, request.Descricao);

                    if (cliente.ValidarTransacaoDeDebito(request.Tipo)) return Results.UnprocessableEntity("O saldo está inconsistente.");
                    
                    var queryInsert = $@"INSERT INTO ""Transacoes"" (""ClienteId"", ""Valor"", ""Tipo"", ""Descricao"") VALUES (@ClienteId, @Valor, @Tipo, @Descricao);";
                    var queryUpdate = $@"UPDATE ""Clientes"" SET ""SaldoInicial"" = {cliente.SaldoInicial} WHERE ""Id"" = @id;";

                    var transacaoAnonima = new { ClienteId = id, Valor = valor, request.Tipo, request.Descricao };

                    var linhasAfetadasInsert = await connection.ExecuteAsync(queryInsert, transacaoAnonima);
                    var linhasAfetadasUpdate = await connection.ExecuteAsync(queryUpdate, new { id });

                    if (linhasAfetadasInsert <= 0) return Results.Problem($"Erro durante o processamento da transação: problema ao inserir transação no banco de dados", null, 500);
                    if (linhasAfetadasUpdate <= 0) return Results.Problem($"Erro durante o processamento da transação: problema ao atualizar cliente no banco de dados", null, 500);

                    var resposta = new
                    {
                        cliente.Limite,
                        Saldo = cliente.SaldoInicial
                    };

                    semaphore.Release();

                    await connection.CloseAsync();

                    return Results.Ok(resposta);
                }
                catch (Exception ex)
                {
                    return Results.Problem($"Erro durante o processamento da transação: {ex.Message}", null, 500);
                }
                finally
                {
                    semaphore.Release();
                }
            })
            .WithName("CadastrarTransacao")
            .WithOpenApi();

            app.MapGet("/clientes/{id}/extrato", async (int id, [FromServices]IConfiguration config, CancellationToken cancellationToken) =>
            {
                try
                {
                    using var connection = new NpgsqlConnection(config.GetConnectionString("DB"));

                    await connection.OpenAsync(cancellationToken);

                    var query = $@"SELECT c.""Id"", c.""Limite"", c.""SaldoInicial"", t.""ClienteId"", t.""Valor"", t.""Tipo"", t.""Descricao"", t.""Data""
                                FROM ""Clientes"" c 
                                LEFT JOIN ""Transacoes"" t 
                                ON t.""ClienteId"" = c.""Id""
                                WHERE c.""Id"" = {id} 
                                ORDER BY t.""Data"" DESC 
                                LIMIT 10"
                    ;

                    var resultado = await connection.QueryAsync<Cliente, Transacao, Cliente>(query, (cliente, transacao) =>
                    {
                        cliente.AdicionarTransacao(transacao);
                        return cliente;
                    },
                    splitOn: "ClienteId");

                    if (!resultado.Any()) return Results.NotFound();

                    var cliente  = resultado.First();

                    var extrato = new
                    {
                        Saldo = new { Total = cliente?.SaldoInicial, Data_extrato = DateTime.Now, cliente?.Limite },
                        Ultimas_transacoes = resultado?
                                                .SelectMany(t => t.Transacoes)?
                                                .Select(t => new { t?.Valor, t?.Tipo, t?.Descricao, Realizada_em = t?.Data })?
                                                .OrderByDescending(t => t.Realizada_em)
                    };

                    await connection.CloseAsync();

                    return Results.Ok(extrato);
                }
                catch (Exception ex)
                {
                    return Results.Problem($"Erro durante o processamento do extrato: {ex.Message}", null, 500);
                }
            })
            .WithName("BuscarExtrato")
            .WithOpenApi();

            return app;
        }
    }
}
