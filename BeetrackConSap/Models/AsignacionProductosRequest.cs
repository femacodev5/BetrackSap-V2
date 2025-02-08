namespace BeetrackConSap.Models {
    public class AsignacionProductosRequest {
        public List<ProductoAsignado> Productos { get; set; }
        public string PickeadorId { get; set; }
        public string IdPlan { get; set; }
        public string IDPick {  get; set; }
    }
}
