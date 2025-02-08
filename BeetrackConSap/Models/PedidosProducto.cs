namespace BeetrackConSap.Models {
    public class PedidosProducto {
        public string IDProducto { get; set; }
        public string Descripcion { get; set; }
        public int Cantidad { get; set; }
        public string NumeroGuia { get; set; }
        public int IDPlanPed {  get; set; }
        public int CantidadBase { get; set; }
        public int Factor { get; set; }
        public int CantidadCargar {  get; set; }
    }
}
