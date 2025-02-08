namespace MorosidadWeb.Models {
    public abstract class VentaBaseVM {
    }
    public class VentaConConsolidado:VentaBaseVM {
        public string Ndocumento { get; set; }
        public string Entry { get; set; }
        public string Posici { get; set; }
        public string Latitud { get; set; }
        public string Longitud { get; set; }
        public string Direccion { get; set; }
        public string Cantidad { get; set; }
        public string Nombreitem { get; set; }
        public string Codigoitem { get; set; }
        public string Fechaminentrega { get; set; }
        public string Fechamaxentrega { get; set; }
        public string Minventanahoraria1 { get; set; }
        public string Maxventanahoraria1 { get; set; }
        public string Costoxkilo { get; set; }
        public string Costoitem { get; set; }
        public string Capacidaduno { get; set; }
        public string Identificadorcontacto { get; set; }
        public string Nombrecontacto { get; set; }
        public string Telefono { get; set; }
        public string Emailcontacto { get; set; }
        public string Ctorigen { get; set; }
        public string Vendedor { get; set; }
        public string Name { get; set; }
        public decimal Factor { get; set; }
        public decimal Pesototal { get; set; }
        public decimal Utilidadtotal { get; set; }
        public decimal Utilidad {  get; set; }
        public int Existe {  get; set; }
        public string Almacen {  get; set; }
        public decimal Stock {  get; set; }
        public int Alertastock { get; set; }
        public string Medidabase { get; set; }
    }
    public class Programacion : VentaBaseVM {
        public string Idpro { get; set; }
        public string Placa { get; set; }
        public string Fecha { get; set; }
        public string Zonasrep { get; set; }

    }
    public class VerRegistrado : VentaBaseVM {
        public string Idregistro { get; set; }
        public string Codigo { get; set; }
        public string Fecha { get; set; }
    }
    public class VerConManifiesto : VentaBaseVM { 
        public string Nummanifiesto { get; set; }
        public string Estadomanifiesto { get; set; }
        public string Placa {  get; set; }
        public string Conductor { get; set; }
        public string Factura { get; set; }
        public string Numerofactura { get; set; }
        public string Numerosolicitud {  get; set; }
        public string Fecha { get; set; }
        public string Total {  get; set; }
        public string Estadosolicitud { get; set; }

    }
    public class VerResumido : VentaBaseVM {
        public string Nummani { get; set; }
        public string Nombregrupo { get; set; }
        public string Ndocumento { get; set; }
        public string Entry { get; set; }
        public string Direccion { get; set; }
        public string Identificadorcontacto { get; set; }
        public string Nombrecontacto { get; set; }
        public string Telefono { get; set; }
        public string Vendedor { get; set; }
        public string Name { get; set; }
        public string Terri { get; set; }
        public string Utilidad { get; set; }
        public string Pesototal { get; set; }

    }
    public class VerVendedor : VentaBaseVM {
        public string Ndocumento { get; set; }
        public string Nombregrupo { get; set; }
        public string Entry { get; set; }
        public string Direccion { get; set; }
        public string Identificadorcontacto {  get; set; }
        public string Nombrecontacto { get;set; }
        public string Telefono { get; set;}
        public string Vendedor { get; set;}
        public string Name { get; set; }
        public string Terri { get; set; }
        public string Openqty { get; set; }
        public string Address {  get; set; }
        public string Docdate { get; set; }
        public string Shipdate { get; set; }
        public string Nombreitem { get; set; }
        public string Cantidad { get; set; }
        public string Codigoitem { get; set; }
        public string Costoitem { get; set; }
        public string Peso {  get; set; }
        public string Utilidad { get; set; }
        public string Latitud { get; set; }
        public string Longitud { get; set; }
        public string Alerta { get; set; }

    }
    public record MorosidadVM(int Id,DateTime Fecha,string Detalle,bool Aprobado);
}