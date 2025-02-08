namespace MorosidadWeb.Models {
    public class MoraCredito {
        public int Id { get; set; }
        public string DocIdentidad { get; set; }
        public string NumCp { get; set; }
        public string PersonaNombre { get; set; }
        public string Telefono { get; set; }
        public string Vendedor { get; set; }
        public DateTime FechaEmision { get; set; }
        public DateTime FechaVencimiento { get; set; }
        public decimal Total { get; set; }
        public decimal Saldo { get; set; }
        public int DiasVencidos { get; set; }
        public int PKID { get; set; }
        public string GrupoVenta { get; set; }
        public string origen { get; set; }
    }
    public record MoraPorGrupo(int PKID,string GrupoVenta,decimal NoVencido,decimal Hoy,decimal D1A4,decimal D5A8,decimal D9A30,decimal Mas30,decimal Morosidad) {
        public decimal Total => NoVencido+Hoy+D1A4+D5A8+D9A30+Mas30;
        public decimal Mora => Morosidad/Total;
    }
}