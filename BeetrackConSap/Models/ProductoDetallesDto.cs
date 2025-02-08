namespace BeetrackConSap.Models {
    public class ProductoDetallesDto {
        public string IDProducto { get; set; }
        public decimal TotalCantidad { get; set; }
        public string Descripcion { get; set; }
        public string MedidaBase { get; set; }
        public string Fabricante { get; set; }
        public string CodigoFabricante { get; set; }
        public string AbsEntry { get; set; }
        public int Pesado { get; set; }
        public string SL1Code { get; set; }
        public string SL2Code { get; set; }
        public string SL3Code { get; set; }
        public string SL4Code { get; set; }
        public decimal PesoTotal { get; set; }
        public string Ubicacion { get; set; }
        public int IDPProducto { get; set; }

        public List<MedidaProductoDto> Medidas { get; set; } = new List<MedidaProductoDto>();
    }
}
