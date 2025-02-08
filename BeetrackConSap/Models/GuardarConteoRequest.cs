using MorosidadWeb.Models;

namespace BeetrackConSap.Models {
    public class GuardarConteoRequest {
        public Dictionary<string, Dictionary<string, List<Registro>>> vehiculoDataConEstado { get; set; }
        public List<string> codigosConCoincidencia { get; set; }
    }

}
