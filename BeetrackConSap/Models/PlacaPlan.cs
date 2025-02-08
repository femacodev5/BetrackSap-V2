namespace BeetrackConSap.Models {
    public class PlacaPlan {
        public string Placa {  get; set; }
        public string IDPlanPla {  get; set; }
        public string Usuario {  get; set; }
        public int IDPick {  get; set; }
        public string Nombre { get; set; }
        public string Jefe { get; set; }
        public int Sap {  get; set; }
        public int Items { get; set; }
        public int Finalizados { get; set; }
        public int Enviado { get; set; }
        public int Cargar {  get; set; }
        public int Cargado { get; set; }
        public int Revision { get; set; }
        public int Finalizado {  get; set; }
        public int Pendientes {  get; set; }
        public int MaxFinalizado { get; set; }
        public int MinFinalizado { get; set; }
        public int Acontar { get; set; }
        public int Contados { get; set; }
        public decimal AcontarTotal { get; set; }
        public decimal ContadosTotal { get; set; }
        public int Verificados { get; set; }
        public int VerificadosPickador { get; set; }
        public int Completo { get; set; }
        public int Recontados { get; set; }
        public int Aceptados { get; set; }
        public int Completos { get; set; }
        public int Recontad {  get; set; }
        public decimal PesoTotal { get; set; }
        public int TotalItems { get; set; }
        public DateTime FechaInicio { get; set; }
        public DateTime FechaTermino { get; set; }
        public DateTime FechaIni {  get; set; }
        public DateTime FechaFi {  get; set; }
        public DateTime FechaVeriIni {  get; set; }
        public DateTime FechaVeriFin { get; set; }
        public TimeSpan TotalDemora {
            get {
                return FechaTermino - FechaInicio;  
            }
        }
        public TimeSpan TotalDemoraPlaca {
            get {
                return FechaFi - FechaIni;
            }
        }

    }
}
