namespace BeetrackConSap.Models {
    public class ProductosContador {
        public string IDProducto { get; set; }
        public string IDPProducto { get; set; }
        public int Cantidad {  get; set; }
        public int Marcado { get; set; }
        public int Finalizado { get; set; }
        public int Aceptado { get; set; }
        public int Cargado { get; set; }
        public int Verificado { get; set; }
        public int CantidadContada { get; set; }
        public string Descripcion { get; set; }
        public string Ubicacion { get; set; }
        public string SL1Code { get; set; }
        public string SL2Code { get; set; }
        public string SL3Code { get; set; }
        public string SL4Code { get; set; }
        public string Fabricante { get; set; }
        public string Iniciado { get; set; }
        public int Estado { get; set; }
        public string CodigoFabricante { get; set; }
        public int AbsEntry { get; set; }
        public string CodigoBarras { get; set; }
        public int Reconteo { get; set; }
        public int Confirma { get; set; }
        public int CargaIncompleta { get; set; }
        public int Confirmado { get; set; }
        public decimal StockGuardado { get; set; }
        public decimal PesoUnidad { get; set; }
        public decimal StockAct {  get; set; }
        public string MedidaBase { get; set; }
        public List<PaqueteInfo> Paquetes { get; set; }

        public ProductosContador() {
            Paquetes = new List<PaqueteInfo>();
        }
        
    }
}
