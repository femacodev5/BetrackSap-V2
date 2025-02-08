namespace BeetrackConSap.Models {
    public class CoordinadorProductos {
        public string IDProducto { get; set; }
        public int TotalCantidad { get; set; }
        public string Descripcion { get; set; }
        public string MedidaBase { get; set; }
        public string Fabricante { get; set; }
        public int Asignado { get; set; }
        public int Pickado { get; set; }
        public int Cargado { get; set; }
        public int Revisado { get; set; }
        public string NuevaUbicacion { get; set; }
        public int Reconteo { get; set; }
        public int Finalizado { get; set; }
        public int Final {  get; set; }

    }
}
