using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;

namespace rinha_backend_24.Dominio.Entidades
{
    [Table("Clientes")]
    public class Cliente
    {
        public Cliente()
        {

        }

        public Cliente(int limite)
        {
            AlterarLimite(limite);
        }

        public int Id { get; init; }
        public int Limite { get; private set; }
        public int SaldoInicial { get; private set; } = 0;

        private readonly List<Transacao> _transacoes = [];
        public IReadOnlyCollection<Transacao> Transacoes => _transacoes;

        public Cliente AlterarLimite(int limite)
        {
            if (Limite == limite) return this;

            Limite = limite;

            return this;
        }

        public Cliente AdicionarNovaTransacao(int valor, string tipo, string descricao)
        {
            var novaTransacao = new Transacao(valor, tipo, descricao);

            switch (tipo)
            {
                case "c":
                    SaldoInicial += valor;
                    break;
                case "d":
                    SaldoInicial -= valor;
                    break;
                default:
                    throw new InvalidOperationException();
            }

            _transacoes.Add(novaTransacao);

            return this;
        }

        public bool ValidarTransacaoDeDebito(string tipo)
        {
            if (tipo == "d")
            {
                return SaldoInicial < Limite * (-1);
            }

            return false;
        }

        public void AdicionarTransacao(Transacao transacao)
        {
            _transacoes.Add(transacao);
        }
    }
}
