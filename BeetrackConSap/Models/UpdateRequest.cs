namespace BeetrackConSap.Models {
    public class UpdateRequest {
        public List<PaqueteriaActualizarDto> Registros { get; set; }
        public int IdpProducto { get; set; }
        public string IdProducto { get; set;}
        public int IdPlan {  get; set;}
    }

}
