namespace BeetrackConSap.Models {
    public class TransferenciasDetalle {
        public int IDPlanPed { get; set; }
        public string DocEntry { get; set; }
        public string DocNum { get; set; }
        public string IDPlanMan { get; set; }
        public string IDPlanPla { get; set; }
        public string Dscription { get; set; }
        public string LineNum { get; set; }
        public string Quantity { get; set; }
        public string ItemCode { get; set; }
        public string DocDueDate { get; set; }
        public string FromWhsCod { get; set; }
        public string WhsCode { get; set; }
        public string U_MSS_ALMDE { get; set; }
        public decimal Peso { get; set; }
        public string Descripcion { get; set; }
        public string NumeroGuia { get; set; }
        public string Ubicacion { get; set; }
        public string MedidaBase { get; set; }
        public string Fabricante { get; set; }
        public decimal CantidadCargar { get; set; }
        public bool RevisadoCoor { get; set; } 
        public string AbsEntry { get; set; }
        public string SL1Code { get; set; }
        public string SL2Code { get; set; }
        public string SL3Code { get; set; }
        public string SL4Code { get; set; }
        public int CantidadFinal { get; set; }
        public bool EstadoFinal { get; set; } 
        public bool Pesado { get; set; } 
        public string CodigoFabricante { get; set; }
        public decimal Factor { get; set; }
        public int CantidadBase { get; set; }
        public decimal StockActual { get; set; }
        public string Linea { get; set; }
        public string CodigoBarras { get; set; }
    }
}
