namespace BeetrackConSap.Models {
    public class ProductosIngresados {
        public int idpProducto { get; set; }  
        public int factor { get; set; }        
        public string idMedida { get; set; }   
        public int cantidad { get; set; }      
        public string absEntry { get; set; }
        public string ubiAbs {  get; set; }
        public decimal stockGuardado { get; set; }
    }

}
