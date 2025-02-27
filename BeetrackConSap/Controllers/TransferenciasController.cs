using BeetrackConSap.Models;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using MorosidadWeb.Models;
using Newtonsoft.Json;
using NPOI.SS.Formula.Functions;
using Org.BouncyCastle.Asn1.Ocsp;
using Sap.Data.Hana;
using System.Data;
using System.Data.SqlClient;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json.Nodes;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace BeetrackConSap.Controllers {
    public class TransferenciasController : Controller {
        private string usuario;
        private string clave;
        private string puesto;
        private string idpickador;
        private readonly string _hanaConnectionString;
        private readonly string _connectionString;

        public TransferenciasController(IConfiguration configuration) {
            _connectionString = configuration.GetConnectionString("Femaco10");
            _hanaConnectionString = configuration.GetConnectionString("FemacoSap");
        }

        public IActionResult PlanificacionTransferencias() {
            return View("~/Views/Transferencias/PlanificacionTransferencias.cshtml");
        }
        public IActionResult EnviadasTransferencias() {
            return View("~/Views/Transferencias/EnviadasTransferencias.cshtml");
        }
        public IActionResult HistotialTransferencias() {
            return View("~/Views/Transferencias/HistotialTransferencias.cshtml");
        }
        public IActionResult PlanProductosTransferencias() {
            return View("~/Views/Transferencias/PlanProductosTransferencias.cshtml");
        }
        public IActionResult Index() {
            return View();
        }

        private void ObtenerUsuarioYClave() {
            string userDataJson = User.FindFirstValue(ClaimTypes.UserData);

            if (!string.IsNullOrEmpty(userDataJson)) {
                dynamic user = JsonConvert.DeserializeObject<dynamic>(userDataJson);
                usuario = user.usuario;
                clave = user.clave;
                Console.WriteLine("Usuario SAP para planificacion: " + usuario);
                Console.WriteLine("Clave SAP para planificacion: " + clave);
            } else {
                Console.WriteLine("No se encontraron datos de ClaimTypes.UserData.");
            }
        }


        public async Task<IActionResult> ObtenerTransferencias(string fechaVencimiento, string terri, string rpt, string znrpt) {

            ObtenerUsuarioYClave();
            HanaConnection hanaConnection = new(_hanaConnectionString);
            hanaConnection.Open();

            string almacen = null;
            string sappQuery = $@"
                            SELECT ""Warehouse"" FROM OUDG WHERE ""Code"" = '{usuario}'";
            using (var hanaCommand = new HanaCommand(sappQuery, hanaConnection)) {
                using (var reader = await hanaCommand.ExecuteReaderAsync()) {
                    if (await reader.ReadAsync()) {
                        almacen = reader["Warehouse"].ToString();
                        Console.WriteLine($"Almacén obtenido: {almacen}");
                    } else {
                        return StatusCode(500, new { message = "No se encontró el almacén para el usuario" });
                    }
                }
            }

                    var str = $@"
            select 
    T1.""DocNum"" as ""Ndocumento"",
    T1.""DocEntry"" as ""Entry"",
    T0.""LineNum"" as ""Posici"",
    T0.""Dscription"" as ""Nombreitem"",
    T0.""OpenCreQty"" ,
    T0.""ItemCode"" as ""Codigoitem"",
    T0.""ShipDate"" as ""Fechaminentrega"" ,
    T1.""DocDueDate"" as ""Fechamaxentrega"",
    T0.""GrssProfit"" as ""Utilidad"",
    T0.""Weight1"",
    T0.""LicTradNum"",
    T1.""CardName"",
    T0.""NumPerMsr"",
    T1.""Weight"" as ""Capacidaduno"",
    T1.""GrosProfit"",
    T1.""Filler"" as ""Almacen"",
    T1.""ToWhsCode"" as ""Ctorigen"",
    T1.""U_MSS_ALMDE"" as ""Direccion"",
    T0.""Quantity"" as ""Cantidad""
from WTQ1 T0 inner join OWTQ T1 on T0.""DocEntry""=T1.""DocEntry""
where T1.""DocDueDate"" = '{fechaVencimiento}' AND T0.""FromWhsCod""='{almacen}'AND T1.""DocStatus""='O' ";

                    Console.WriteLine(str);
                    IEnumerable<VentaConConsolidado> ventaConConsolidadoSap = hanaConnection.Query<VentaConConsolidado>(str);
                    hanaConnection.Close();

                    var connection = new SqlConnection(_connectionString);
                    await connection.OpenAsync();

            foreach (var item in ventaConConsolidadoSap) {

                var sqlQuery = @"
                            SELECT 
                                CASE 
                                    WHEN COUNT(*) = 0 THEN 0
                                    WHEN MAX(COALESCE(Planeado, 0)) = 0 THEN 1
                                    WHEN MAX(COALESCE(Planeado, 0)) = 1 THEN 2
                                    ELSE 0 
                                END AS Existe
                            FROM PedidosBeetrack
                            WHERE Codigo = @Codigo";

                var existe = await connection.ExecuteScalarAsync<int>(sqlQuery, new { Codigo = item.Ndocumento });
                item.Existe = existe;
            }
            ventaConConsolidadoSap = ventaConConsolidadoSap.Where(item => item.Existe != 2).ToList();

            await connection.CloseAsync();

                    return Ok(ventaConConsolidadoSap);            
        }



        [HttpPost]
        public async Task<IActionResult> GenerarTransferenciaProvincia([FromBody] Dictionary<string, PlacaProvincia> datos) {

            int count = 0;
            try {


                
                if (datos == null || !datos.Any()) {
                    return BadRequest(new { message = "No se han recibido datos válidos." });
                }
                ObtenerUsuarioYClave();

                HanaConnection hanaConnection = new(_hanaConnectionString);
                string codigo = DateTime.Now.ToString("yyMMddHHmmss");
                string almacen = null;
                bool direccionDiferente = false;
                string direccionAnterior = null;

                foreach (var placa in datos) {
                    foreach (var item in placa.Value.Pedidos) {
                        Console.WriteLine(item.direccion);

                        if (direccionAnterior != null && item.direccion != direccionAnterior) {
                            direccionDiferente = true;
                            break; 
                        }

                        direccionAnterior = item.direccion; 

                        count++;
                    }

                    if (direccionDiferente) {
                        return BadRequest(new { message = "El destino de la transferencia debe ser Igual" });
                    }
                }

                foreach (var placa in datos) {
                    using (var connection = new SqlConnection(_connectionString)) {

                            await hanaConnection.OpenAsync();
                        await connection.OpenAsync();

                        almacen = placa.Value.Pedidos[0].almacen;

                        string queryPlan = "INSERT INTO PlanificacionTraslado (FechaTraslado, AlmacenTraslado, UsuarioTraslado, ClaveTraslado) OUTPUT INSERTED.IDPlanTraslado VALUES (GETDATE(), @Almacen, @Usuario, @Clave)";
                        using (SqlCommand cmdPlan = new SqlCommand(queryPlan, connection)) {
                            cmdPlan.Parameters.AddWithValue("@Almacen", almacen);
                            cmdPlan.Parameters.AddWithValue("@Usuario", usuario);
                            cmdPlan.Parameters.AddWithValue("@Clave", clave);

                            int idPlan = (int)await cmdPlan.ExecuteScalarAsync();

                            string queryCodigo = "INSERT INTO  CodigosBeetrack  (Codigo, Fecha, Almacen, IDPlan, ExcelSubido, Tipo) VALUES (@Codigo, GETDATE(), @Almacen, @IDPlan, 1, 'Transferencia')";
                            using (SqlCommand cmdCodigo = new SqlCommand(queryCodigo, connection)) {
                                cmdCodigo.Parameters.AddWithValue("@Codigo", codigo);
                                cmdCodigo.Parameters.AddWithValue("@Almacen", almacen);
                                cmdCodigo.Parameters.AddWithValue("@IDPlan", idPlan);
                                await cmdCodigo.ExecuteNonQueryAsync();
                            }

                            string queryPlaca = "INSERT INTO PlanificacionPlacaTransferencia(IDPlan, Placa, Capacidad) OUTPUT INSERTED.IDPlanPla VALUES(@IDPlan, @placa, @Capacidad)";
                            using (SqlCommand cmdPlaca = new SqlCommand(queryPlaca, connection)) {
                                cmdPlaca.Parameters.AddWithValue("@IDPlan", idPlan);
                                cmdPlaca.Parameters.AddWithValue("@Placa", placa.Key);
                                cmdPlaca.Parameters.AddWithValue("@Capacidad", placa.Value.Capacidad);

                                int idPlanPla = (int)await cmdPlaca.ExecuteScalarAsync();

                                string queryMani = "INSERT INTO PlacaManifiestoTransferencia(IDPlanPla, Numero) VALUES(@IDPlanPla, 1)";
                                using (SqlCommand cmdMani = new SqlCommand(queryMani, connection)) {
                                    cmdMani.Parameters.AddWithValue("@IDPlanPla", idPlanPla);
                                    await cmdMani.ExecuteNonQueryAsync();
                                }

                                string querySelect = "SELECT IDPlanMan FROM PlacaManifiestoTransferencia WHERE IDPlanPla = @IDPlanPla";
                                using (SqlCommand cmdSelect = new SqlCommand(querySelect, connection)) {
                                    cmdSelect.Parameters.AddWithValue("@IDPlanPla", idPlanPla);

                                    var result = await cmdSelect.ExecuteScalarAsync();
                                    int idPlanMan = Convert.ToInt32(result);

                                    foreach (var pedido in placa.Value.Pedidos) {
                                        pedido.factor = 1; // eliminar esto
                                        double cantidadDouble = double.Parse(pedido.cantidad);
                                        int cantidadmul = (int)Math.Round(cantidadDouble);
                                        decimal pesotot = decimal.Parse(pedido.capacidaduno);
                                        decimal cantpes = cantidadmul * pedido.factor;
                                        decimal cantidadFinal = cantidadmul * pedido.factor;
                                        decimal pesofinal = pesotot / cantpes;
                                        string idProducto = pedido.codigoitem;


                                        string binCode = "";
                                        string sl1code = "";
                                        string sl2code = "";
                                        string sl3code = "";
                                        string sl4code = "";
                                        string absentry = "";
                                        string invntryUom = "";
                                        string fabricante = null;
                                        string tipopeso = null;
                                        string codigofabricante = null;
                                        string stockactual = null;
                                        string linea = null;
                                        string codebars = null;

                                        string sapQuery = $@"
                                            SELECT 
                                                T1.""BinCode"",
                                                T1.""SL1Code"",
                                                T1.""SL2Code"",
                                                T1.""SL3Code"",
                                                T1.""SL4Code"",
                                                T1.""AbsEntry"",
                                                T2.""InvntryUom"",
                                                T3.""FirmName"",
                                                T2.""U_FEM_TipoPeso"",
                                                T2.""SuppCatNum"",
                                                T2.""CodeBars"",
                                                T4.""OnHand"",
                                                T5.""Name""
                                            FROM OIBQ T0
                                            INNER JOIN OBIN T1 ON T0.""BinAbs"" = T1.""AbsEntry""
                                            INNER JOIN OITM T2 ON T2.""ItemCode"" = T0.""ItemCode""
                                            INNER JOIN OMRC T3 ON T3.""FirmCode"" = T2.""FirmCode""
                                            INNER JOIN OITW T4 ON T4.""ItemCode"" = T0.""ItemCode"" AND T4.""WhsCode"" = '{almacen}'
                                            INNER JOIN ""@EXF_SUBFAM"" T5 ON T5.""Code"" = T2.""U_FEM_SUBFAM""
                                            WHERE T0.""ItemCode"" = '{idProducto}' AND T0.""WhsCode"" = '{almacen}' AND T0.""OnHandQty""> 0
                                            UNION
                                            SELECT 
                                                T0.""BinCode"",
                                                T0.""SL1Code"",
                                                T0.""SL2Code"",
                                                T0.""SL3Code"",
                                                T0.""SL4Code"",
                                                T0.""AbsEntry"",
                                                T2.""InvntryUom"",
                                                T3.""FirmName"",
                                                T2.""U_FEM_TipoPeso"",
                                                T2.""SuppCatNum"",
                                                T2.""CodeBars"",
                                                T4.""OnHand"",
                                                T5.""Name""
                                            FROM OBIN T0
                                            LEFT JOIN OIBQ T1 ON T1.""BinAbs"" = T0.""AbsEntry"" AND T1.""ItemCode"" = '{idProducto}'
                                            LEFT JOIN OITM T2 ON T2.""ItemCode"" = T1.""ItemCode""
                                            LEFT JOIN OMRC T3 ON T3.""FirmCode"" = T2.""FirmCode"" 
                                            LEFT JOIN OITW T4 ON T4.""ItemCode"" = T2.""ItemCode"" AND T4.""WhsCode"" = '{almacen}'
                                            LEFT JOIN ""@EXF_SUBFAM"" T5 ON T5.""Code"" = T2.""U_FEM_SUBFAM""
                                            WHERE T0.""AbsEntry"" = (SELECT ""DftBinAbs"" FROM OITW WHERE ""WhsCode"" = '{almacen}' AND ""ItemCode"" = '{idProducto}')
                                            ";

                                        try {
                                            using (var hanaCommand = new HanaCommand(sapQuery, hanaConnection)) {
                                                using (var reader = await hanaCommand.ExecuteReaderAsync()) {
                                                    if (await reader.ReadAsync()) {
                                                        binCode = reader["BinCode"].ToString();
                                                        sl1code = reader["SL1Code"].ToString();
                                                        sl2code = reader["SL2Code"].ToString();
                                                        sl3code = reader["SL3Code"].ToString();
                                                        sl4code = reader["SL4Code"].ToString();
                                                        absentry = reader["AbsEntry"].ToString();
                                                        invntryUom = reader["InvntryUom"].ToString();
                                                        fabricante = reader["FirmName"] != DBNull.Value ? reader["FirmName"].ToString() : null;
                                                        tipopeso = reader["U_FEM_TipoPeso"].ToString();
                                                        codigofabricante = reader["SuppCatNum"].ToString();
                                                        stockactual = reader["OnHand"].ToString();
                                                        linea = reader["Name"].ToString();
                                                        codebars = reader["CodeBars"].ToString();
                                                        Console.WriteLine($"SAP: BinCode = {binCode}, InvntryUom = {invntryUom} para ItemCode = {idProducto}");
                                                    } else {
                                                        Console.WriteLine($"No se encontraron resultados en SAP para ItemCode = {idProducto}");
                                                    }
                                                }
                                            }
                                        } catch (Exception sapEx) {
                                            Console.WriteLine($"Error al consultar SAP para ItemCode = {idProducto}: {sapEx.Message}");
                                        }


                                        if (sl1code != "UBICACIÓN-DE-SssssssISTEMA") {
                                            string queryPlacaPedido = "INSERT INTO PlacaPedidoTransferencia (IDPlanMan, IDPlanPla, IDProducto, Cantidad, Peso, Descripcion, NumeroGuia, DocNum, LineNum, Ubicacion, MedidaBase, Fabricante, AbsEntry, SL1Code, SL2Code, SL3Code, SL4Code, Pesado, CodigoFabricante, Factor, CantidadBase, StockActual, Linea, CodigoBarras) VALUES (@IDPlanMan, @IDPlanPla,@IDProducto, @Cantidad, @Peso, @Descripcion, @NumeroGuia, @DocNum, @LineNum, @Ubicacion, @MedidaBase, @Fabricante, @AbsEntry, @Sl1Code, @Sl2Code, @Sl3Code, @Sl4Code, @TipoPeso, @CodigoFabricante, @Factor, @CantidadBase, @StockActual, @Linea, @CodigoBarras);";
                                            using (SqlCommand cmdPlaPedido = new SqlCommand(queryPlacaPedido, connection)) {
                                                cmdPlaPedido.Parameters.AddWithValue("@IDPlanMan", idPlanMan);
                                                cmdPlaPedido.Parameters.AddWithValue("@IDPlanPla", idPlanPla);
                                                cmdPlaPedido.Parameters.AddWithValue("@IDProducto", pedido.codigoitem);
                                                cmdPlaPedido.Parameters.AddWithValue("@Cantidad", cantidadFinal);
                                                cmdPlaPedido.Parameters.AddWithValue("@Peso", pesofinal);
                                                cmdPlaPedido.Parameters.AddWithValue("@Descripcion", pedido.nombreitem);
                                                cmdPlaPedido.Parameters.AddWithValue("@NumeroGuia", pedido.ndocumento);
                                                cmdPlaPedido.Parameters.AddWithValue("@DocNum", pedido.entry);
                                                cmdPlaPedido.Parameters.AddWithValue("@LineNum", pedido.posici);
                                                cmdPlaPedido.Parameters.AddWithValue("@Ubicacion", binCode);
                                                cmdPlaPedido.Parameters.AddWithValue("@MedidaBase", invntryUom);
                                                cmdPlaPedido.Parameters.AddWithValue("@Fabricante", string.IsNullOrEmpty(fabricante) ? (object)DBNull.Value : fabricante);
                                                cmdPlaPedido.Parameters.AddWithValue("@AbsEntry", absentry);
                                                cmdPlaPedido.Parameters.AddWithValue("@SL1Code", sl1code);
                                                cmdPlaPedido.Parameters.AddWithValue("@SL2Code", sl2code);
                                                cmdPlaPedido.Parameters.AddWithValue("@SL3Code", sl3code);
                                                cmdPlaPedido.Parameters.AddWithValue("@SL4Code", sl4code);
                                                cmdPlaPedido.Parameters.AddWithValue("@TipoPeso", string.IsNullOrEmpty(tipopeso) ? (object)DBNull.Value : tipopeso);
                                                cmdPlaPedido.Parameters.AddWithValue("@CodigoFabricante", string.IsNullOrEmpty(codigofabricante) ? (object)DBNull.Value : codigofabricante);
                                                cmdPlaPedido.Parameters.AddWithValue("@Factor", pedido.factor);
                                                cmdPlaPedido.Parameters.AddWithValue("@CantidadBase", cantidadmul);
                                                cmdPlaPedido.Parameters.AddWithValue("@StockActual", string.IsNullOrEmpty(stockactual) ? (object)DBNull.Value : stockactual);
                                                cmdPlaPedido.Parameters.AddWithValue("@Linea", string.IsNullOrEmpty(linea) ? (object)DBNull.Value : linea);
                                                cmdPlaPedido.Parameters.AddWithValue("@CodigoBarras", string.IsNullOrEmpty(codebars) ? (object)DBNull.Value : codebars);
                                                await cmdPlaPedido.ExecuteNonQueryAsync();
                                            }
                                        }
                                    }
                                }

                            }


                        }

                        await hanaConnection.CloseAsync();

                        string lastNdocumento = string.Empty;
                        int contpedidos = 0;
                        foreach (var pedido in placa.Value.Pedidos) {
                            if (pedido.ndocumento != lastNdocumento) {
                                lastNdocumento = pedido.ndocumento;
                                contpedidos++;
                                Console.WriteLine(pedido.utilidadtotal);
                                string queryPedido = "INSERT INTO PedidosBeetrack (Codigo, Fecha, General, Planeado, Direccion, Utilidad) VALUES (@Codigo, GETDATE(), @General, 1, @Direccion, @Utilidad)";
                                using (SqlCommand cmdPedido = new SqlCommand(queryPedido, connection)) {
                                    cmdPedido.Parameters.AddWithValue("@Codigo", pedido.ndocumento);
                                    cmdPedido.Parameters.AddWithValue("@General", codigo);
                                    cmdPedido.Parameters.AddWithValue("@Direccion", pedido.direccion);
                                    cmdPedido.Parameters.AddWithValue("@Utilidad", pedido.utilidadtotal);
                                    await cmdPedido.ExecuteNonQueryAsync();
                                }
                            }
                        }

                        string queryupcb = "UPDATE CodigosBeetrack SET Totales = @Cantidad,  Planeados = @Cantidad, Libres = 0 WHERE Codigo = @Codigo";
                        using (SqlCommand cmdUpcb = new SqlCommand(queryupcb, connection)) {
                            cmdUpcb.Parameters.AddWithValue("@Cantidad", contpedidos);
                            cmdUpcb.Parameters.AddWithValue("@Codigo", codigo);
                            await cmdUpcb.ExecuteNonQueryAsync();
                        }
                    }
                }

                return Ok(new { message = "Los datos se recibieron correctamente." });
            } catch (Exception ex) {
                return StatusCode(500, new { message = "Ocurrió un error al procesar la solicitud.", error = ex.Message });
            }
        }




        public async Task<IActionResult> ObtenerTransferenciasDetalle(string fechaVencimiento, string docnum) {
            if (string.IsNullOrEmpty(fechaVencimiento) || string.IsNullOrEmpty(docnum)) {
                return BadRequest("Se requieren datos obligatorios.");
            }

            using (HanaConnection hanaConnection = new(_hanaConnectionString)) {
                try {
                    hanaConnection.Open();

                    var str = @"
        select 
T0.""DocNum"",
T0.""DocEntry"",
T1.""Dscription"",
T1.""LineNum"",
T1.""Quantity"",
T1.""ItemCode"",
T0.""DocDueDate"",
T1.""FromWhsCod"",
T1.""WhsCode"",
T0.""U_MSS_ALMDE""
from OWTQ T0 inner join WTQ1 T1 on T0.""DocEntry""=T1.""DocEntry"" where T0.""DocDueDate""=?  AND T0.""DocNum"" = ?";

                    var transferenciasDetalleAbiertas = await hanaConnection.QueryAsync<TransferenciasDetalle>(
                        str, new { fechaVencimiento = fechaVencimiento, docnum = docnum });

                    hanaConnection.Close();
                    return Ok(transferenciasDetalleAbiertas);
                } catch (Exception ex) {
                    Console.WriteLine($"Error al obtener datos de HANA: {ex.Message}");
                    return StatusCode(500, "Hubo un error al obtener los datos.");
                }
            }
        }


        public async Task<IActionResult> ObtenerTransferenciasDetalleEnvio(string fechaVencimiento, string docnum) {
            if (string.IsNullOrEmpty(fechaVencimiento) || string.IsNullOrEmpty(docnum)) {
                return BadRequest("Se requieren datos obligatorios.");
            }

            using (HanaConnection hanaConnection = new(_hanaConnectionString)) {
                try {
                    await hanaConnection.OpenAsync();

                    var str = $@"
        select 
            T1.""DocNum"" as ""Ndocumento"",
            T1.""DocEntry"" as ""Entry"",
            T0.""LineNum"" as ""Posici"",
            T1.""U_MSS_ALMDE"" as ""Direccion"",
            T0.""Dscription"" as ""Nombreitem"",
            T0.""OpenCreQty"",
            T0.""ItemCode"" as ""Codigoitem"",
            T0.""ShipDate"",
            T1.""DocDueDate"" as ""Fechamaxentrega"",
            T0.""GrssProfit"",
            T0.""Weight1"",
            T0.""LicTradNum"",
            T1.""CardName"",
            T0.""NumPerMsr"",
            T1.""Weight"" as ""capacidaduno"",
            T1.""GrosProfit"",
            T1.""Filler"" as ""Almacen"",
            T1.""ToWhsCode"" as ""Ctorigen"",
            T1.""U_MSS_ALMDE"",
            T0.""Quantity"" as ""Cantidad""
        from WTQ1 T0 
        inner join OWTQ T1 on T0.""DocEntry"" = T1.""DocEntry""
        where T1.""DocDueDate"" = '{fechaVencimiento}' and T1.""DocNum"" ='{docnum}' ";

                    var transferenciasDetalleAbiertas = await hanaConnection.QueryAsync<VentaConConsolidado>(str);

                    return Ok(transferenciasDetalleAbiertas);
                } catch (Exception ex) {
                    Console.WriteLine($"Error al obtener datos de HANA: {ex.Message}");
                    return StatusCode(500, "Hubo un error al obtener los datos.");
                }
            }
        }







        [HttpGet]
        public async Task<IActionResult> ObtenerPlanificacionesEnviadasTransferencias() {

            var connection = new SqlConnection(_connectionString);
            HanaConnection hanaConnection = new(_hanaConnectionString);

            await hanaConnection.OpenAsync();
            await connection.OpenAsync();
            ObtenerUsuarioYClave();
            string almacen = null;

            string sappQuery = $@"
                SELECT ""Warehouse"" FROM OUDG WHERE ""Code"" = '{usuario}'";
            using (var hanaCommand = new HanaCommand(sappQuery, hanaConnection)) {
                using (var reader = await hanaCommand.ExecuteReaderAsync()) {
                    if (await reader.ReadAsync()) {
                        almacen = reader["Warehouse"].ToString();
                        Console.WriteLine($"Almacén obtenido: {almacen}");
                    } else {
                        return StatusCode(500, new { message = "No se encontró el almacén para el usuario" });
                    }
                }
            }

            string query = @"SELECT 
                T1.IDPlan,
                T1.Codigo,
                T1.Fecha,
                T1.Totales,
                T1.Planeados,
                T1.Libres,
                T1.ExcelSubido,
                T1.Almacen,
                T1.Tipo,
                T3.Placa,
                T3.IDPlanPla,
                T3.Usuario,
                T3.CargaIncompleta,
                T3.Confirmado,
                T3.Capacidad,
                T4.IDPick,
                T5.Nombre,
                T6.Nombre AS Jefe,
                (SELECT COUNT(P6.Codigo) FROM PedidosBeetrack P6 WHERE P6.General = T1.Codigo) AS TotalPrev,
				(SELECT COUNT(DISTINCT P1.IDProducto) FROM PlacaPedidoTransferencia P1 WHERE P1.IDPlanPla = T3.IDPlanPla) AS TotalItems,
				(SELECT SUM(P4.Factor*P4.Cantidad) FROM PickeoProductoIngresado P4 INNER JOIN PickeoProducto P5 ON P4.IDPProducto = P5.IDPProducto WHERE P5.IDPlaca = T3.IDPlanPla) AS PickadosReal,
				(SELECT SUM(P0.Cantidad) FROM PlacaPedidoTransferencia P0 WHERE P0.IDPlanPla = T3.IDPlanPla) AS TotalPick,
                (SELECT COUNT(P1.IDProducto) FROM PickeoProducto P1 WHERE P1.IDPlaca = T3.IDPlanPla) AS Items,
                (SELECT COUNT(P1.IDProducto) FROM PickeoProducto P1 WHERE P1.IDPlaca = T3.IDPlanPla AND P1.Finalizado = 1) AS Finalizados,
                (SELECT COUNT(P1.IDProducto) FROM PlacaPedidoTransferencia P1 WHERE P1.IDPlanPla = T3.IDPlanPla AND P1.RevisadoCoor = 1) AS RevisadosCoor,
                (SELECT COUNT(P1.IDProducto) FROM PlacaPedidoTransferencia P1 WHERE P1.IDPlanPla = T3.IDPlanPla) AS Pedidos,
                (SELECT TOP 1 P2.IDPick FROM PlacaManifiestoTransferencia P2 WHERE P2.IDPlanPla = T3.IDPlanPla) AS IDPicks,
                (SELECT SUM(P3.Cantidad*P3.Peso) FROM PlacaPedidoTransferencia P3 WHERE P3.IDPlanPla = T3.IDPlanPla) AS PesoCarga,
				(SELECT SUM(P1.Peso*P1.Cantidad) FROM PlacaPedidoTransferencia P1 INNER JOIN PickeoProducto P2 ON P2.IDPlaca = P1.IDPlanPla  AND P2.IDProducto = P1.IDProducto WHERE P1.IDPlanPla = T3.IDPlanPla AND P2.IDPick = T4.IDPick) AS PesoTotalPick,
                (SELECT COUNT(P2.IDProducto) FROM PickeoProducto P2 WHERE P2.IDPlaca = T3.IDPlanPla AND P2.IDPick = T4.IDPick) AS TotalItemsPick,
                T7.Pendientes,
                (SELECT SUM(P1.Cantidad) FROM PlacaPedidoTransferencia P1 INNER JOIN PickeoProducto P2 ON P2.IDProducto = P1.IDProducto WHERE P1.IDPlanPla = T3.IDPlanPla AND P2.IDPick = T4.IDPick) AS Acontar,
                (SELECT COUNT(P1.IDProducto) FROM PickeoProducto P1 WHERE P1.IDPlaca = T3.IDPlanPla AND P1.Finalizado = 1 AND P1.Reconteo = 1 AND P1.IDPick = T4.IDPick) AS Recontad,
                (SELECT COUNT(P1.IDProducto) FROM PickeoProducto P1 WHERE P1.IDPlaca = T3.IDPlanPla AND P1.Finalizado = 1 AND P1.Reconteo = 1 AND P1.Aceptado = null) AS Recontados,
                (SELECT MIN(P1.FechaCre) FROM PickeoProducto P1 WHERE P1.IDPlaca = T3.IDPlanPla AND P1.IDPick = T4.IDPick) AS FechaInicio,
                (SELECT MAX(P1.FechaFin) FROM PickeoProducto P1 WHERE P1.IDPlaca = T3.IDPlanPla AND P1.IDPick = T4.IDPick) AS FechaTermino,
                T3.FechaInicio AS FechaIni,
                T3.FechaFin AS FechaFi,
                SUM(T8.Cantidad*T8.Factor) AS Contados,
                T3.Enviado,
                T3.Cargar,
                T3.Cargado,
                T3.Revision,
                T3.Sap,
                T3.LastMileCodigo
                FROM CodigosBeetrack T1
                LEFT JOIN PlanificacionTraslado T2 ON T2.IDPlanTraslado = T1.IDPlan
                LEFT JOIN PlanificacionPlacaTransferencia T3 ON T2.IDPlanTraslado = T3.IDPlan
                LEFT JOIN PickeoProducto T4 ON T4.IDPlaca = T3.IDPlanPla
                LEFT JOIN PickeoPersonal T5 ON T5.IDPP = T4.IDPick
                LEFT JOIN PickeoPersonal T6 ON T6.IDPP = T3.Usuario
                LEFT JOIN (
                SELECT 
                R1.IDPlanPla,
                COUNT(R3.IDProducto) AS Pendientes
                FROM PlanificacionTraslado R0
                INNER JOIN PlanificacionPlacaTransferencia R1 ON R0.IDPlanTraslado = R1.IDPlan
                INNER JOIN PlacaManifiestoTransferencia R2 ON R1.IDPlanPla = R2.IDPlanPla
                INNER JOIN PlacaPedidoTransferencia R3 ON R2.IDPlanMan = R3.IDPlanMan
                LEFT JOIN PickeoProducto R4 ON R4.IDProducto = R3.IDProducto AND R4.IDPlaca = R1.IDPlanPla
                WHERE 
                R4.IDProducto IS NULL 
                GROUP BY R1.IDPlanPla) T7 ON T7.IDPlanPla = T3.IDPlanPla
                LEFT JOIN PickeoProductoIngresado T8 ON T8.IDPProducto = T4.IDPProducto
                WHERE T1.Almacen = @almacen and T1.Tipo='Transferencia' and  T3.Placa is not null
                GROUP BY T1.IDPlan, T1.Tipo, T1.Codigo, T1.Fecha, T1.Totales, T1.Planeados, T1.Libres, T1.ExcelSubido, T1.Almacen, T3.Placa, T3.Capacidad, T3.IDPlanPla, T3.Usuario, T4.IDPick, T5.Nombre, T6.Nombre, T7.Pendientes, T3.Confirmado,T3.CargaIncompleta,T3.Enviado,T3.Cargar,T3.Cargado,T3.Revision,T3.Sap, T3.LastMileCodigo, 
T3.FechaInicio, T3.FechaFin order by T1.Fecha asc
";

            var result = await connection.QueryAsync(query, new { almacen });
            var filteredResult = new List<dynamic>();

            foreach (var row in result.ToList()) {
                string manifiesto = "SIN MANIFIESTO";

                string manifestQuery = @"
                    SELECT T1.""DocEntry""
                    FROM ""@EXP_MANLP"" T0
                    INNER JOIN ""@EXP_MANC"" T1 ON T1.""DocEntry"" = T0.""DocEntry""
                    WHERE T0.""U_EXP_PKLO"" = :IDPicks";

                using (var hanaCommand = new HanaCommand(manifestQuery, hanaConnection)) {
                    hanaCommand.Parameters.Add(new HanaParameter(":IDPicks", row.IDPicks));

                    using (var reader = await hanaCommand.ExecuteReaderAsync()) {
                        if (await reader.ReadAsync()) {
                            manifiesto = reader["DocEntry"].ToString();
                        }
                    }
                }
                row.Manifiesto = manifiesto;
                filteredResult.Add(row);


            }

            await hanaConnection.CloseAsync();
            await connection.CloseAsync();

            return Ok(filteredResult);
        }
        [HttpGet]
        public async Task<IActionResult> ObtenerPlanificacionesEnviadasTransferenciasHistorial() {

            var connection = new SqlConnection(_connectionString);
            HanaConnection hanaConnection = new(_hanaConnectionString);

            await hanaConnection.OpenAsync();
            await connection.OpenAsync();
            ObtenerUsuarioYClave();
            string almacen = null;

            string sappQuery = $@"
                SELECT ""Warehouse"" FROM OUDG WHERE ""Code"" = '{usuario}'";
            using (var hanaCommand = new HanaCommand(sappQuery, hanaConnection)) {
                using (var reader = await hanaCommand.ExecuteReaderAsync()) {
                    if (await reader.ReadAsync()) {
                        almacen = reader["Warehouse"].ToString();
                        Console.WriteLine($"Almacén obtenido: {almacen}");
                    } else {
                        return StatusCode(500, new { message = "No se encontró el almacén para el usuario" });
                    }
                }
            }

            string query = @"SELECT 
                T1.IDPlan,
                T1.Codigo,
                T1.Fecha,
                T1.Totales,
                T1.Planeados,
                T1.Libres,
                T1.ExcelSubido,
                T1.Almacen,
                T1.Tipo,
                T3.Placa,
                T3.IDPlanPla,
                T3.Usuario,
                T3.CargaIncompleta,
                T3.Confirmado,
                T3.Capacidad,
                T4.IDPick,
                T5.Nombre,
                T6.Nombre AS Jefe,
                (SELECT COUNT(P6.Codigo) FROM PedidosBeetrack P6 WHERE P6.General = T1.Codigo) AS TotalPrev,
				(SELECT COUNT(DISTINCT P1.IDProducto) FROM PlacaPedidoTransferencia P1 WHERE P1.IDPlanPla = T3.IDPlanPla) AS TotalItems,
				(SELECT SUM(P4.Factor*P4.Cantidad) FROM PickeoProductoIngresado P4 INNER JOIN PickeoProducto P5 ON P4.IDPProducto = P5.IDPProducto WHERE P5.IDPlaca = T3.IDPlanPla) AS PickadosReal,
				(SELECT SUM(P0.Cantidad) FROM PlacaPedidoTransferencia P0 WHERE P0.IDPlanPla = T3.IDPlanPla) AS TotalPick,
                (SELECT COUNT(P1.IDProducto) FROM PickeoProducto P1 WHERE P1.IDPlaca = T3.IDPlanPla) AS Items,
                (SELECT COUNT(P1.IDProducto) FROM PickeoProducto P1 WHERE P1.IDPlaca = T3.IDPlanPla AND P1.Finalizado = 1) AS Finalizados,
                (SELECT COUNT(P1.IDProducto) FROM PlacaPedidoTransferencia P1 WHERE P1.IDPlanPla = T3.IDPlanPla AND P1.RevisadoCoor = 1) AS RevisadosCoor,
                (SELECT COUNT(P1.IDProducto) FROM PlacaPedidoTransferencia P1 WHERE P1.IDPlanPla = T3.IDPlanPla) AS Pedidos,
                (SELECT TOP 1 P2.IDPick FROM PlacaManifiestoTransferencia P2 WHERE P2.IDPlanPla = T3.IDPlanPla) AS IDPicks,
                (SELECT SUM(P3.Cantidad*P3.Peso) FROM PlacaPedidoTransferencia P3 WHERE P3.IDPlanPla = T3.IDPlanPla) AS PesoCarga,
				(SELECT SUM(P1.Peso*P1.Cantidad) FROM PlacaPedidoTransferencia P1 INNER JOIN PickeoProducto P2 ON P2.IDPlaca = P1.IDPlanPla  AND P2.IDProducto = P1.IDProducto WHERE P1.IDPlanPla = T3.IDPlanPla AND P2.IDPick = T4.IDPick) AS PesoTotalPick,
                (SELECT COUNT(P2.IDProducto) FROM PickeoProducto P2 WHERE P2.IDPlaca = T3.IDPlanPla AND P2.IDPick = T4.IDPick) AS TotalItemsPick,
                T7.Pendientes,
                (SELECT SUM(P1.Cantidad) FROM PlacaPedidoTransferencia P1 INNER JOIN PickeoProducto P2 ON P2.IDProducto = P1.IDProducto WHERE P1.IDPlanPla = T3.IDPlanPla AND P2.IDPick = T4.IDPick) AS Acontar,
                (SELECT COUNT(P1.IDProducto) FROM PickeoProducto P1 WHERE P1.IDPlaca = T3.IDPlanPla AND P1.Finalizado = 1 AND P1.Reconteo = 1 AND P1.IDPick = T4.IDPick) AS Recontad,
                (SELECT COUNT(P1.IDProducto) FROM PickeoProducto P1 WHERE P1.IDPlaca = T3.IDPlanPla AND P1.Finalizado = 1 AND P1.Reconteo = 1 AND P1.Aceptado = null) AS Recontados,
                (SELECT MIN(P1.FechaCre) FROM PickeoProducto P1 WHERE P1.IDPlaca = T3.IDPlanPla AND P1.IDPick = T4.IDPick) AS FechaInicio,
                (SELECT MAX(P1.FechaFin) FROM PickeoProducto P1 WHERE P1.IDPlaca = T3.IDPlanPla AND P1.IDPick = T4.IDPick) AS FechaTermino,
                T3.FechaInicio AS FechaIni,
                T3.FechaFin AS FechaFi,
                SUM(T8.Cantidad*T8.Factor) AS Contados,
                T3.Enviado,
                T3.Cargar,
                T3.Cargado,
                T3.Revision,
                T3.Sap,
                T3.LastMileCodigo
                FROM CodigosBeetrack T1
                LEFT JOIN PlanificacionTraslado T2 ON T2.IDPlanTraslado = T1.IDPlan
                LEFT JOIN PlanificacionPlacaTransferencia T3 ON T2.IDPlanTraslado = T3.IDPlan
                LEFT JOIN PickeoProducto T4 ON T4.IDPlaca = T3.IDPlanPla
                LEFT JOIN PickeoPersonal T5 ON T5.IDPP = T4.IDPick
                LEFT JOIN PickeoPersonal T6 ON T6.IDPP = T3.Usuario
                LEFT JOIN (
                SELECT 
                R1.IDPlanPla,
                COUNT(R3.IDProducto) AS Pendientes
                FROM PlanificacionTraslado R0
                INNER JOIN PlanificacionPlacaTransferencia R1 ON R0.IDPlanTraslado = R1.IDPlan
                INNER JOIN PlacaManifiestoTransferencia R2 ON R1.IDPlanPla = R2.IDPlanPla
                INNER JOIN PlacaPedidoTransferencia R3 ON R2.IDPlanMan = R3.IDPlanMan
                LEFT JOIN PickeoProducto R4 ON R4.IDProducto = R3.IDProducto AND R4.IDPlaca = R1.IDPlanPla
                WHERE 
                R4.IDProducto IS NULL 
                GROUP BY R1.IDPlanPla) T7 ON T7.IDPlanPla = T3.IDPlanPla
                LEFT JOIN PickeoProductoIngresado T8 ON T8.IDPProducto = T4.IDPProducto
                WHERE T1.Almacen = @almacen and T1.Tipo='Transferencia' and  T3.Placa is not null
                GROUP BY T1.IDPlan, T1.Tipo, T1.Codigo, T1.Fecha, T1.Totales, T1.Planeados, T1.Libres, T1.ExcelSubido, T1.Almacen, T3.Placa, T3.Capacidad, T3.IDPlanPla, T3.Usuario, T4.IDPick, T5.Nombre, T6.Nombre, T7.Pendientes, T3.Confirmado,T3.CargaIncompleta,T3.Enviado,T3.Cargar,T3.Cargado,T3.Revision,T3.Sap, T3.LastMileCodigo, 
T3.FechaInicio, T3.FechaFin order by T1.Fecha asc
";

            var result = await connection.QueryAsync(query, new { almacen });
            var filteredResult = new List<dynamic>();

            foreach (var row in result.ToList()) {
                string manifiesto = "SIN MANIFIESTO";

                string manifestQuery = @"
                    SELECT T1.""DocEntry""
                    FROM ""@EXP_MANLP"" T0
                    INNER JOIN ""@EXP_MANC"" T1 ON T1.""DocEntry"" = T0.""DocEntry""
                    WHERE T0.""U_EXP_PKLO"" = :IDPicks";

                using (var hanaCommand = new HanaCommand(manifestQuery, hanaConnection)) {
                    hanaCommand.Parameters.Add(new HanaParameter(":IDPicks", row.IDPicks));

                    using (var reader = await hanaCommand.ExecuteReaderAsync()) {
                        if (await reader.ReadAsync()) {
                            manifiesto = reader["DocEntry"].ToString();
                        }
                    }
                }
                row.Manifiesto = manifiesto;
                if ((row.Tipo == "Transferencia" && row.Manifiesto != "SIN MANIFIESTO")) {
                    filteredResult.Add(row);
                }

            }

            await hanaConnection.CloseAsync();
            await connection.CloseAsync();

            return Ok(filteredResult);
        }
        public async Task<IActionResult> ObtenerTransferenciasDetalleEnviados([FromQuery] int idPlanPla) {
            if (idPlanPla <= 0) {
                return BadRequest("El IDPlanPla no es válido.");
            }

            using (var connection = new SqlConnection(_connectionString)) {
                try {
                    await connection.OpenAsync();

                    string query = "SELECT * FROM PlacaPedidoTransferencia WHERE IDPlanPla = @idPlanPla;";

                    using (var cmd = new SqlCommand(query, connection)) {
                        cmd.Parameters.AddWithValue("@idPlanPla", idPlanPla);

                        using (var reader = await cmd.ExecuteReaderAsync()) {
                            if (!reader.HasRows) {
                                return NotFound("No se encontraron detalles para el IDPlanPla proporcionado.");
                            }

                            var results = new List<dynamic>();
                            while (await reader.ReadAsync()) {
                                var row = new {
                                    IDProducto = reader["IDProducto"],
                                    Descripcion = reader["Descripcion"],
                                    DocNum = reader["DocNum"],
                                };

                                results.Add(row);
                            }

                            return Ok(results);
                        }
                    }
                } catch (Exception ex) {
                    Console.WriteLine($"Error al obtener los datos: {ex.Message}");
                    return StatusCode(500, "Hubo un error al obtener los datos.");
                }
            }
        }

        [HttpDelete]
        public ActionResult BorrarSubidaProvinciaTransferencia(string codigo, int plan) {
            try {
                using (var connection = new SqlConnection(_connectionString)) {
                    connection.Open();

                    string deletePedidosQuery = "DELETE FROM PedidosBeetrack WHERE General = @Codigo";
                    using (var command = new SqlCommand(deletePedidosQuery, connection)) {
                        command.Parameters.AddWithValue("@Codigo", codigo);
                        command.ExecuteNonQuery();
                    }

                    string deleteCodigosQuery = "DELETE FROM CodigosBeetrack WHERE Codigo = @Codigo and Tipo = 'Transferencia'" ;
                    using (var command = new SqlCommand(deleteCodigosQuery, connection)) {
                        command.Parameters.AddWithValue("@Codigo", codigo);
                        command.ExecuteNonQuery();
                    }
                    string deleteplanificacionQuery = "DELETE FROM PlanificacionTraslado WHERE IDPlanTraslado = (SELECT IDPlan FROM PlanificacionPlacaTransferencia WHERE IDPlanPla = @Plan)";
                    using (var command = new SqlCommand(deleteplanificacionQuery, connection)) {
                        command.Parameters.AddWithValue("@Plan", plan);
                        command.ExecuteNonQuery();
                    }
                    string deleteplanificacionplaQuery = "DELETE FROM PlanificacionPlacaTransferencia WHERE IDPlanPla = @Plan";
                    using (var command = new SqlCommand(deleteplanificacionplaQuery, connection)) {
                        command.Parameters.AddWithValue("@Plan", plan);
                        command.ExecuteNonQuery();
                    }
                    string deleteplanificacionmanQuery = "DELETE FROM PlacaManifiestoTransferencia WHERE IDPlanPla = @Plan";
                    using (var command = new SqlCommand(deleteplanificacionmanQuery, connection)) {
                        command.Parameters.AddWithValue("@Plan", plan);
                        command.ExecuteNonQuery();
                    }

                    string deleteplanificacionpedidoQuery = "DELETE FROM PlacaPedidoTransferencia WHERE IDPlanPla = @Plan";
                    using (var command = new SqlCommand(deleteplanificacionpedidoQuery, connection)) {
                        command.Parameters.AddWithValue("@Plan", plan);
                        command.ExecuteNonQuery();
                    }

                    return Json(new { success = true, message = "Subida borrada correctamente." });
                }
            } catch (Exception ex) {
                return Json(new { success = false, message = "Hubo un error al borrar la subida.", error = ex.Message });
            }
        }



        [HttpPost]
        public async Task<IActionResult> GuardarJefePlacaTransferencia(int idPlanPla, int idPickeador) {
            string query = null;
            if (idPickeador == 0) {
                query = @"
                    UPDATE PlanificacionPlacaTransferencia SET Usuario = NULL, FechaJefe = GETDATE() WHERE IDPlanPla = @idPlanPla";
            } else {
                query = @"
                    UPDATE PlanificacionPlacaTransferencia SET Usuario = @idPickeador, FechaJefe = GETDATE() WHERE IDPlanPla = @idPlanPla";
            }
            using (var connection = new SqlConnection(_connectionString)) {
                await connection.OpenAsync();
                var parameters = new { idPickeador, idPlanPla };

                var result = await connection.ExecuteAsync(query, parameters);

                if (result > 0) {
                    return Ok(new { success = true, message = "Jefe asignado exitosamente." });
                } else {
                    return NotFound(new { success = false, message = "No se encontraron registros para actualizar." });
                }
            }
        }
        [HttpGet]
        public async Task<IActionResult> ObtenerProductoPlan(int ID) {
            using (var connection = new SqlConnection(_connectionString)) {
                string query = @"
                    SELECT 
T3.IDProducto,
SUM(T3.Cantidad) AS TotalCantidad,
SUBSTRING(T3.Descripcion,0,46) AS Descripcion,
T3.MedidaBase,
SUBSTRING(T3.Fabricante,0,14) AS Fabricante,
SUBSTRING(T3.CodigoFabricante,0,8) AS CodigoFabricante,
T3.Pesado,
T3.SL1Code,
T3.SL2Code,
T3.SL3Code,
T3.SL4Code,
(SELECT SUM(P1.Peso*P1.Cantidad) FROM PlacaPedido P1 WHERE P1.IDPlanPla = T1.IDPlanPla AND P1.IDProducto = T3.IDProducto) AS PesoTotal,
(SELECT SUM(P1.Peso*P1.Cantidad) FROM PlacaPedido P1 WHERE P1.IDPlanPla = T1.IDPlanPla) AS PesoVenta,
T5.Nombre,
T1.Placa,
T1.Capacidad,
(SELECT COUNT(DISTINCT P1.IDProducto) FROM PlacaPedido P1 WHERE P1.IDPlanPla = T1.IDPlanPla) AS TotalItems
FROM PlanificacionTraslado T0
INNER JOIN PlanificacionPlacaTransferencia T1 ON T0.IDPlanTraslado = T1.IDPlan
INNER JOIN PlacaManifiestoTransferencia T2 ON T1.IDPlanPla = T2.IDPlanPla
INNER JOIN PlacaPedidoTransferencia T3 ON T2.IDPlanMan = T3.IDPlanMan
LEFT JOIN PickeoProducto T4 ON T4.IDProducto = T3.IDProducto AND T4.IDPlaca = T1.IDPlanPla
INNER JOIN PickeoPersonal T5 ON T5.IDPP = T1.Usuario
WHERE 
T1.IDPlanPla = @IDPlan
AND T4.IDProducto IS NULL
GROUP BY 
T1.IDPlanPla,
T3.IDProducto,
T3.Descripcion,
T3.MedidaBase,
T3.Fabricante,
T3.CodigoFabricante,
T3.Pesado,
T3.SL1Code,
T3.SL2Code,
T3.SL3Code,
T3.SL4Code,
T5.Nombre,
T1.Placa,
T1.Capacidad
ORDER BY Pesado, SL1Code, SL2Code, SL3Code, SL4Code";

                var parameters = new { IDPlan = ID };
                var result = await connection.QueryAsync<ProductoPlanData>(query, parameters);
                return Ok(result);
            }
        }



        [HttpGet]
        public async Task<IActionResult> CargarPersonalPickeo(string id, string almacenubicacion) {
            using (var connection = new SqlConnection(_connectionString)) {
                string queryUsuario = @"
                    SELECT Usuario FROM PlanificacionPlacaTransferencia WHERE IDPlanPla = @id";
                var usuario = await connection.QueryFirstOrDefaultAsync<string>(queryUsuario, new { id });

                if (usuario == null) {
                    return NotFound("Usuario no encontrado.");
                }
                string queryPickeoPersonal = @"SELECT IDPP, Nombre FROM PickeoPersonal WHERE (Puesto = 3 OR IDPP = @usuario) AND Almacen = @almacenubicacion";

                var result = await connection.QueryAsync(queryPickeoPersonal, new { usuario, almacenubicacion });
                return Ok(result);
            }
        }
        [HttpPost]
        public async Task<IActionResult> GuardarProductosAsignados([FromBody] AsignacionProductosRequest request) {
            if (request.Productos == null || !request.Productos.Any()) {
                return BadRequest("No se han proporcionado productos para asignar.");
            }

            using (var connection = new SqlConnection(_connectionString)) {
                await connection.OpenAsync();

                using (var transaction = await connection.BeginTransactionAsync()) {
                    try {
                        string updateQuery = @"
                            UPDATE PlanificacionPlacaTransferencia 
                            SET FechaInicio = GETDATE() 
                            WHERE IDPlanPla = @idplan AND FechaInicio IS NULL";

                        var updateParameters = new {
                            idplan = request.IdPlan
                        };
                        await connection.ExecuteAsync(updateQuery, updateParameters, transaction);

                        foreach (var producto in request.Productos) {
                            string insertQuery = @"
                                INSERT INTO PickeoProducto (IDPlaca, IDPick, IDProducto, Cantidad)
                                VALUES (@IDPlan, @IDPickeador, @IDProducto, @Cantidad)";

                            var parameters = new {
                                IDProducto = producto.Id,
                                Cantidad = producto.Cantidad,
                                IDPickeador = request.PickeadorId,
                                IDPlan = request.IdPlan
                            };
                            if (parameters.Cantidad != "") {
                                await connection.ExecuteAsync(insertQuery, parameters, transaction);
                            }
                        }

                        await transaction.CommitAsync();
                        return Ok(new { success = true });
                    } catch (Exception ex) {
                        await transaction.RollbackAsync();
                        return StatusCode(500, $"Error al guardar los productos asignados: {ex.Message}");
                    }
                }
            }
        }


        [HttpGet]
        public async Task<IActionResult> ObtenerPlacaPlanJefeTransferencias(int ID) {
            using (var connection = new SqlConnection(_connectionString)) {
                string query = @"
                    SELECT 
T1.Placa,
T1.IDPlanPla,
T1.Usuario,
T2.IDPick,
T4.Nombre,
T5.Nombre AS Jefe,
T1.Enviado,
(SELECT COUNT(P1.IDProducto) FROM PickeoProducto P1 WHERE P1.IDPlaca = T1.IDPlanPla) AS Items,
(SELECT COUNT(P1.IDProducto) FROM PickeoProducto P1 WHERE P1.IDPlaca = T1.IDPlanPla AND P1.Finalizado = 1) AS Finalizados,
(SELECT COUNT(P1.IDProducto) FROM PickeoProducto P1 WHERE P1.IDPlaca = T1.IDPlanPla AND P1.Verificado = 1) AS Verificados,
(SELECT COUNT(P1.IDProducto) FROM PickeoProducto P1 WHERE P1.IDPlaca = T1.IDPlanPla AND P1.Verificado = 1 AND P1.IDPick = T2.IDPick) AS VerificadosPickador,
(SELECT COUNT(P1.IDProducto) FROM PickeoProducto P1 WHERE P1.IDPlaca = T1.IDPlanPla AND P1.Finalizado = 1 AND P1.Reconteo = 1 AND P1.IDPick = T2.IDPick) AS Recontad,
(SELECT COUNT(P1.IDProducto) FROM PickeoProducto P1 WHERE P1.IDPlaca = T1.IDPlanPla AND P1.Finalizado = 1 AND P1.Reconteo = 1 AND P1.Aceptado = null) AS Recontados,
(SELECT COUNT(P1.IDProducto) FROM PickeoProducto P1 WHERE P1.IDPlaca = T1.IDPlanPla AND P1.Finalizado = 1 AND P1.Reconteo = 1 AND P1.Aceptado = 1) AS Aceptados,
(SELECT COUNT(P1.IDProducto) FROM PickeoProducto P1 WHERE P1.IDPlaca = T1.IDPlanPla AND P1.Cantidad = (SELECT SUM(T1.Factor*T1.Cantidad) FROM PickeoProductoIngresado T1 WHERE T1.IDPProducto = P1.IDPProducto)) AS Completos,
(SELECT MIN(P1.FechaCre) FROM PickeoProducto P1 WHERE P1.IDPlaca = T1.IDPlanPla AND P1.IDPick = T2.IDPick) AS FechaInicio,
(SELECT MAX(P1.FechaFin) FROM PickeoProducto P1 WHERE P1.IDPlaca = T1.IDPlanPla AND P1.IDPick = T2.IDPick) AS FechaTermino,
(SELECT MIN(P1.FechaVeriIni) FROM PickeoProducto P1 WHERE P1.IDPlaca = T1.IDPlanPla AND P1.IDPick = T2.IDPick) AS FechaVeriIni,
(SELECT MIN(P1.FechaVeriFin) FROM PickeoProducto P1 WHERE P1.IDPlaca = T1.IDPlanPla AND P1.IDPick = T2.IDPick) AS FechaVeriFin,
(SELECT SUM(P1.Peso*P1.Cantidad) FROM PlacaPedidoTransferencia P1 INNER JOIN PickeoProducto P2 ON P2.IDPlaca = P1.IDPlanPla  AND P2.IDProducto = P1.IDProducto WHERE P1.IDPlanPla = T1.IDPlanPla AND P2.IDPick = T2.IDPick) AS PesoTotal,
(SELECT COUNT(P2.IDProducto) FROM PickeoProducto P2 WHERE P2.IDPlaca = T1.IDPlanPla AND P2.IDPick = T2.IDPick) AS TotalItems,
T6.Pendientes,
(SELECT SUM(P1.Cantidad) FROM PlacaPedidoTransferencia P1 INNER JOIN PickeoProducto P2 ON P2.IDProducto = P1.IDProducto AND P2.IDPlaca = P1.IDPlanPla WHERE P1.IDPlanPla = T1.IDPlanPla AND P2.IDPick = T2.IDPick) AS Acontar,
(SELECT SUM(P1.Cantidad) FROM PlacaPedidoTransferencia P1 INNER JOIN PickeoProducto P2 ON P2.IDProducto = P1.IDProducto AND P2.IDPlaca = P1.IDPlanPla WHERE P1.IDPlanPla = T1.IDPlanPla) AS AcontarTotal,
(SELECT SUM(P1.Cantidad*P1.Factor) FROM PickeoProductoIngresado P1 INNER JOIN PickeoProducto P2 ON P2.IDPProducto = P1.IDPProducto WHERE P2.IDPlaca = T1.IDPlanPla) AS ContadosTotal,
SUM(T7.Cantidad*T7.Factor) AS Contados,
T1.Cargar,
T1.Cargado,
T1.FechaInicio AS FechaIni,
T1.FechaFin AS FechaFi
FROM PlanificacionTraslado T0
INNER JOIN PlanificacionPlacaTransferencia T1 ON T0.IDPlanTraslado = T1.IDPlan
LEFT JOIN PickeoProducto T2 ON T2.IDPlaca = T1.IDPlanPla
LEFT JOIN PickeoPersonal T4 ON T4.IDPP = T2.IDPick
LEFT JOIN PickeoPersonal T5 ON T5.IDPP = T1.Usuario
LEFT JOIN (
SELECT 
R1.IDPlanPla,
COUNT(R3.IDProducto) AS Pendientes
FROM PlanificacionTraslado R0
INNER JOIN PlanificacionPlacaTransferencia R1 ON R0.IDPlanTraslado = R1.IDPlan
INNER JOIN PlacaManifiesto R2 ON R1.IDPlanPla = R2.IDPlanPla
INNER JOIN PlacaPedido R3 ON R2.IDPlanMan = R3.IDPlanMan
LEFT JOIN PickeoProducto R4 ON R4.IDProducto = R3.IDProducto AND R4.IDPlaca = R1.IDPlanPla
WHERE 
R4.IDProducto IS NULL
GROUP BY R1.IDPlanPla) T6 ON T6.IDPlanPla = T1.IDPlanPla
LEFT JOIN PickeoProductoIngresado T7 ON T7.IDPProducto = T2.IDPProducto
                    WHERE T5.IDPP = @IDPlan AND T1.FechaFin IS NULL 
  AND (SELECT COUNT(P1.IDProducto) FROM PickeoProducto P1 WHERE P1.IDPlaca = T1.IDPlanPla AND P1.IDProducto != 0) > 0
                    GROUP BY T1.Placa, T1.IDPlanPla, T1.Enviado, T1.Usuario, T2.IDPick, T4.Nombre, T5.Nombre, T6.Pendientes, T1.Cargar, T1.Cargado, T1.FechaInicio, T1.FechaFin ORDER BY T1.FechaInicio
                    ";

                var parameters = new { IDPlan = ID };
                var result = await connection.QueryAsync<PlacaPlan>(query, parameters);
                return Ok(result);
            }
        }


    }
}




























