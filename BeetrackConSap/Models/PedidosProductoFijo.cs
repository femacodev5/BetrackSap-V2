namespace BeetrackConSap.Models {
    public class PedidosProductoFijo {
        public int IDPlanPed {  get; set; }
        public string Descripcion { get; set; }
        public decimal Cantidad { get; set; }
        public int CantidadBase { get; set; }
        public decimal Factor {  get; set; }
        public decimal Peso { get; set; }
        public string IDProducto { get; set; }
        public string NumeroGuia { get; set; }
        public decimal Utilidad { get; set; }
    }
}
