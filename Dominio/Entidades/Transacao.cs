using System.ComponentModel.DataAnnotations.Schema;

namespace rinhabackend24q1.Dominio.Entidades
{
    [Table("Transacoes")]
    public class Transacao
    {
        public Transacao()
        {

        }

        public Transacao(int valor, string tipo, string descricao)
        {
            AlterarValor(valor);
            AlterarTipo(tipo);
            AlterarDescricao(descricao);

            Data = DateTime.Now.ToUniversalTime();
        }

        public int Id { get; init; }
        public int ClienteId { get; private set; }
        public int Valor { get; private set; }
        public string Tipo { get; private set; }
        public string Descricao { get; private set; }
        public DateTime Data { get; init; }

        public Transacao AlterarValor(int valor)
        {
            if (Valor == valor) return this;

            Valor = valor;

            return this;
        }

        public Transacao AlterarTipo(string tipo)
        {
            if (Tipo == tipo) return this;

            Tipo = tipo;

            return this;
        }

        public Transacao AlterarDescricao(string descricao)
        {
            if (Descricao == descricao) return this;

            Descricao = descricao;

            return this;
        }
    }
}
