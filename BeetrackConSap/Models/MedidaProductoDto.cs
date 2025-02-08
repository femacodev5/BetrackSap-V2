namespace BeetrackConSap.Models {
    public class MedidaProductoDto {
        public string Medida { get; set; }  
        public string IdMedida { get; set; }  
        public decimal Factor { get; set; }  
        public decimal Stock { get; set; }

        public decimal CantidadPedida { get; set; }
    }
}
