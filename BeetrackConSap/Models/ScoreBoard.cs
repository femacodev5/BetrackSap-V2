using System.Data;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MorosidadWeb.Models {
    public record Antiguedad(string PKID, DateTime Fecha, string PersonaNombre, string Codigo, string Descripcion);
  

    public record Consumo(int Anio,int Mes, string Total);
    /*[property: JsonConverter(typeof(DecimalJsonConverter))]*/

    public record DiasPago(int Pkid,string NumCp,DateTime FechaEmision,DateTime FechaVencimiento,DateTime FechaPago, int Diferencia, int Credito, string Total, string origen) {
        public bool Aprobado { get; set; }
    }

    public record ListSearch(int PKID,string DocIdentidad,string Nombre);
    //public class ScoreBoard {
    //}
    public class DecimalJsonConverter:JsonConverter<decimal> {

        public override decimal Read(ref Utf8JsonReader reader,Type typeToConvert,JsonSerializerOptions options) {
            throw new NotImplementedException();
        }

        public override void Write(Utf8JsonWriter writer,decimal value,JsonSerializerOptions options) {
            writer.WriteStringValue($"{value:n2}");
        }
    }
    public class ScoreBoard {
        public enum ETipoPersona {
            Natural, Juridica
        }
        public bool IsArequipa { get; set; }
        public ETipoPersona Persona { get; set; }
        public int TipoPersona => Persona==ETipoPersona.Natural ? 80 : 100;

        public int Antiguedad { get; set; }
        public int VolumenCompra { get; set; }
        public int? DiasPagoCredito { get; set; }
        public int? DiasPagoContado { get; set; }

        //public double ScoreTipoPersona => TipoPersona*(IsArequipa ? 0.05 : 0.25);
        //public double ScoreAntiguedad => Antiguedad*(IsArequipa ? 0.1 : 0.25);
        //public double ScoreVolumenCompra => VolumenCompra*(IsArequipa ? 0.15 : 0.2);
        //public double ScoreDiasPago => (DiasPagoCredito+DiasPagoContado)/2*(IsArequipa ? 0.7 : 0.3);

        public double ScoreTipoPersona => TipoPersona*0.05;
        public double ScoreAntiguedad => Antiguedad*0.10;
        public double ScoreVolumenCompra => VolumenCompra*0.05;
        public double ScoreDiasPago => ((DiasPagoCredito??0)+(DiasPagoContado??0))/(DiasPagoContado==null || DiasPagoCredito==null?1:2)*0.8;

        public int SumaScore => TipoPersona+Antiguedad+VolumenCompra+((DiasPagoCredito??0+DiasPagoContado??0)/2);
        public double ScoreFinal => ScoreTipoPersona+ScoreAntiguedad+ScoreVolumenCompra+ScoreDiasPago;

    }
}
