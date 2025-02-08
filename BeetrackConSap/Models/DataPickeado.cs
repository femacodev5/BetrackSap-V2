namespace BeetrackConSap.Models {
    public class DataPickeado {
        public string Nombre { get; set; }
        public int IDPP { get; set; }
        public string IDProducto { get; set; }
        public string Descripcion { get; set; }
        public int IDPProducto { get; set; }
        public int Items { get; set; }
        public int Finalizados { get; set; }
        public int Cantidad { get; set; }
        public int CantidadPicada { get; set; }
        public int Diferencia => Cantidad - CantidadPicada;
    }
}
