namespace BeetrackConSap.Models {
    public class ProductoPlan {
        public string IDProducto { get; set; }
        public string Descripcion { get; set; }
        public string Usuario { get; set; }

        public int TotalCantidad { get; set; }
        public string MedidaBase { get; set; }
        public string Fabricante { get; set; }
        public int Pesado { get; set; }
        public string SL1Code {  get; set; }
        public string SL2Code { get; set; }
        public string SL3Code { get; set; }
        public string SL4Code { get; set;}
        public decimal PesoTotal { get; set; }
        public int Iniciados { get; set; }
        public string Placa {  get; set; }
        public string Capacidad { get; set; }
        public string Linea { get; set; }
        public string Nombre { get; set; }
    }
}
