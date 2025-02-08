using Newtonsoft.Json.Linq;

namespace BeetrackConSap.Models {
    public class VehiculoModel {
        public decimal Peso { get; set; }
        public string Code { get; set; }
        public JObject U_EXX_PESVEH { get; set; }

    }
}
