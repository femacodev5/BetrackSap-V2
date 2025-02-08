using Microsoft.EntityFrameworkCore;

namespace BeetrackConSap.Models {
    [Keyless]
    public class PaqueteriasProducto {
        public string Medida { get; set; }
        public string IdMedida { get; set; }

        [Precision(5, 2)]
        public decimal Factor { get; set; }

        [Precision(5, 2)]
        public decimal Stock { get; set; }

        [Precision(5, 2)]
        public int Recibido { get; set; }
        public int Cantidad { get; set; }
        public decimal StockGuardado { get; set; }
        public string UbiAbs {  get; set; }
        public int CantidadBase { get; set; }
        public int Base {  get; set; }
        public int AbsEntry { get; set; }
    }
}
