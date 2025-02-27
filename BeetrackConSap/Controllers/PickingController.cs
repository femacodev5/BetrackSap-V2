using Microsoft.AspNetCore.Mvc;
using System.Data.SqlClient;
using Sap.Data.Hana;
using Dapper;
using BeetrackConSap.Models;
using NPOI.Util;
using SAPbouiCOM;
using static NPOI.HSSF.Util.HSSFColor;
using Newtonsoft.Json;
using System.Security.Claims;
using NPOI.SS.Formula.Functions;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;
using MySqlX.XDevAPI.Common;
using System.Numerics;


namespace BeetrackConSap.Controllers {
    public class PickingController : Controller {

        private string usuario;
        private string clave;
        private string puesto;
        private string idpickador;
        private readonly string _hanaConnectionString;
        private readonly string _connectionString;

        public PickingController(IConfiguration configuration) {
            _connectionString = configuration.GetConnectionString("Femaco10");
            _hanaConnectionString = configuration.GetConnectionString("FemacoSap");
        }
        public IActionResult Index() {
            return View("~/Views/Picking/Planificacion.cshtml");
        }
        public IActionResult Planificacion() {
            return View("~/Views/Picking/Planificacion.cshtml");
        }
        public IActionResult PickeadorConteo(int idpp) {
            ViewBag.IdPP = idpp;
            return View("~/Views/Picking/PickeadorConteo.cshtml");
        }
        public IActionResult JefeConteo(int idpp) {
            ViewBag.IdPP = idpp;
            return View("~/Views/Picking/JefeConteo.cshtml");
        }
        public IActionResult PickeadorProductos() {
            return View("~/Views/Picking/PickeadorProductos.cshtml");
        }
        public IActionResult VerificarProductos() {
            return View("~/Views/Picking/VerificarProductos.cshtml");
        }

        public IActionResult QuitarPlanProductos() {
            return View("~/Views/Picking/QuitarPlanProductos.cshtml");
        }
        public IActionResult VerificarProductosPickador() {
            return View("~/Views/Picking/VerificarProductosPickador.cshtml");
        }
        public IActionResult ProductosVerificados() {
            return View("~/Views/Picking/ProductosVerificados.cshtml");
        }
        public IActionResult ProductosVerificadosPickador() {
            return View("~/Views/Picking/ProductosVerificadosPickador.cshtml");
        }
        public IActionResult ConteoRevision() {
            return View("~/Views/Picking/ConteoRevision.cshtml");
        }
        public IActionResult PlanPlacas() {
            return View("~/Views/Picking/PlanPlacas.cshtml");
        }
        public IActionResult PickDetalles() {
            return View("~/Views/Picking/PickDetalles.cshtml");
        }
        public IActionResult PlanManifiestos() {
            return View("~/Views/Picking/PlanManifiestos.cshtml");
        }
        public IActionResult PlanProductos() {
            return View("~/Views/Picking/PlanProductos.cshtml");
        }
        public IActionResult PlanAsignados() {
            return View("~/Views/Picking/PlanAsignados.cshtml");
        }

        private void ObtenerUsuarioYClave() {
            string userDataJson = User.FindFirstValue(ClaimTypes.UserData);

            if (!string.IsNullOrEmpty(userDataJson)) {
                dynamic user = JsonConvert.DeserializeObject<dynamic>(userDataJson);
                usuario = user.usuario;
                clave = user.clave;
                puesto = user.puesto;
                idpickador = user.idpp;
                Console.WriteLine("Usuario SAP: " + usuario);
                Console.WriteLine("Clave SAP: " + clave);
                Console.WriteLine("Puesto: " + puesto);
                Console.WriteLine("IDpp: " + idpickador);
            } else {
                Console.WriteLine("No se encontraron datos de ClaimTypes.UserData.");
            }
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerPlanificaciones() {
            using (var connection = new SqlConnection(_connectionString)) {
                string query = @"
                        SELECT IDPlan, Fecha FROM Planificacion";

                var result = await connection.QueryAsync(query);
                return Ok(result);
            }
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerPlacaPlan(int ID) {
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
                    T6.Pendientes,
                    T8.Acontar / T8.Pedidos AS Acontar,
                    SUM(T7.Cantidad*T7.Factor) AS Contados
                    FROM Planificacion T0
                    INNER JOIN PlanificacionPlaca T1 ON T0.IDPlan = T1.IDPlan
                    LEFT JOIN PickeoProducto T2 ON T2.IDPlaca = T1.IDPlanPla
                    LEFT JOIN PickeoPersonal T4 ON T4.IDPP = T2.IDPick
                    LEFT JOIN PickeoPersonal T5 ON T5.IDPP = T1.Usuario
                    LEFT JOIN (
                    SELECT 
                    R1.IDPlanPla,
                    COUNT(R3.IDProducto) AS Pendientes
                    FROM Planificacion R0
                    INNER JOIN PlanificacionPlaca R1 ON R0.IDPlan = R1.IDPlan
                    INNER JOIN PlacaManifiesto R2 ON R1.IDPlanPla = R2.IDPlanPla
                    INNER JOIN PlacaPedido R3 ON R2.IDPlanMan = R3.IDPlanMan
                    LEFT JOIN PickeoProducto R4 ON R4.IDProducto = R3.IDProducto AND R4.IDPlaca = R1.IDPlanPla
                    WHERE 
                    R4.IDProducto IS NULL
                    GROUP BY R1.IDPlanPla) T6 ON T6.IDPlanPla = T1.IDPlanPla
                    LEFT JOIN PickeoProductoIngresado T7 ON T7.IDPProducto = T2.IDPProducto
                    LEFT JOIN (
                    SELECT 
                    M1.IDPlanPla,
                    COUNT(M3.IDProducto) AS Pendientes,
                    SUM(M4.Cantidad) AS Acontar,
                    M4.IDPick,
                    COUNT(M4.IDPick) AS Pedidos
                    FROM Planificacion M0
                    INNER JOIN PlanificacionPlaca M1 ON M0.IDPlan = M1.IDPlan
                    INNER JOIN PlacaManifiesto M2 ON M1.IDPlanPla = M2.IDPlanPla
                    INNER JOIN PlacaPedido M3 ON M2.IDPlanMan = M3.IDPlanMan
                    LEFT JOIN PickeoProducto M4 ON M4.IDProducto = M3.IDProducto AND M4.IDPlaca = M1.IDPlanPla
                    WHERE 
                    M4.IDProducto IS NOT NULL
                    GROUP BY M1.IDPlanPla, M4.IDPick
                    ) T8 ON T8.IDPlanPla = T1.IDPlanPla AND T8.IDPick = T2.IDPick
                    WHERE T0.IDPlan = @IDPlan
                    GROUP BY T1.Placa, T1.IDPlanPla, T1.Enviado, T1.Usuario, T2.IDPick, T4.Nombre, T5.Nombre, T6.Pendientes, T8.Acontar, T8.Pedidos;";

                var parameters = new { IDPlan = ID };
                var result = await connection.QueryAsync<PlacaPlan>(query, parameters);
                return Ok(result);
            }
        }


        [HttpGet]
        public async Task<IActionResult> ObtenerPlacaPlanJefe(int ID) {
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
                    (SELECT SUM(P1.Peso*P1.Cantidad) FROM PlacaPedido P1 INNER JOIN PickeoProducto P2 ON P2.IDPlaca = P1.IDPlanPla  AND P2.IDProducto = P1.IDProducto WHERE P1.IDPlanPla = T1.IDPlanPla AND P2.IDPick = T2.IDPick) AS PesoTotal,
                    (SELECT COUNT(P2.IDProducto) FROM PickeoProducto P2 WHERE P2.IDPlaca = T1.IDPlanPla AND P2.IDPick = T2.IDPick) AS TotalItems,
                    T6.Pendientes,
                    (SELECT SUM(P1.Cantidad) FROM PlacaPedido P1 INNER JOIN PickeoProducto P2 ON P2.IDProducto = P1.IDProducto AND P2.IDPlaca = P1.IDPlanPla WHERE P1.IDPlanPla = T1.IDPlanPla AND P2.IDPick = T2.IDPick) AS Acontar,
                    (SELECT SUM(P1.Cantidad) FROM PlacaPedido P1 INNER JOIN PickeoProducto P2 ON P2.IDProducto = P1.IDProducto AND P2.IDPlaca = P1.IDPlanPla WHERE P1.IDPlanPla = T1.IDPlanPla) AS AcontarTotal,
                    (SELECT SUM(P1.Cantidad*P1.Factor) FROM PickeoProductoIngresado P1 INNER JOIN PickeoProducto P2 ON P2.IDPProducto = P1.IDPProducto WHERE P2.IDPlaca = T1.IDPlanPla) AS ContadosTotal,
                    SUM(T7.Cantidad*T7.Factor) AS Contados,
                    T1.Cargar,
                    T1.Cargado,
                    T1.FechaInicio AS FechaIni,
                    T1.FechaFin AS FechaFi
                    FROM Planificacion T0
                    INNER JOIN PlanificacionPlaca T1 ON T0.IDPlan = T1.IDPlan
                    LEFT JOIN PickeoProducto T2 ON T2.IDPlaca = T1.IDPlanPla
                    LEFT JOIN PickeoPersonal T4 ON T4.IDPP = T2.IDPick
                    LEFT JOIN PickeoPersonal T5 ON T5.IDPP = T1.Usuario
                    LEFT JOIN (
                    SELECT 
                    R1.IDPlanPla,
                    COUNT(R3.IDProducto) AS Pendientes
                    FROM Planificacion R0
                    INNER JOIN PlanificacionPlaca R1 ON R0.IDPlan = R1.IDPlan
                    INNER JOIN PlacaManifiesto R2 ON R1.IDPlanPla = R2.IDPlanPla
                    INNER JOIN PlacaPedido R3 ON R2.IDPlanMan = R3.IDPlanMan
                    LEFT JOIN PickeoProducto R4 ON R4.IDProducto = R3.IDProducto AND R4.IDPlaca = R1.IDPlanPla
                    WHERE 
                    R4.IDProducto IS NULL
                    GROUP BY R1.IDPlanPla) T6 ON T6.IDPlanPla = T1.IDPlanPla
                    LEFT JOIN PickeoProductoIngresado T7 ON T7.IDPProducto = T2.IDPProducto
                    WHERE T5.IDPP = @IDPlan and T1.FechaFin IS NULL
                    GROUP BY T1.Placa, T1.IDPlanPla, T1.Enviado, T1.Usuario, T2.IDPick, T4.Nombre, T5.Nombre, T6.Pendientes, T1.Cargar, T1.Cargado, T1.FechaInicio, T1.FechaFin ORDER BY T1.FechaInicio
                    ";

                var parameters = new { IDPlan = ID };
                var result = await connection.QueryAsync<PlacaPlan>(query, parameters);
                return Ok(result);
            }
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerManifiestoPlan(int ID) {
            using (var connection = new SqlConnection(_connectionString)) {
                string query = @"
                    SELECT 
                    T2.IDPlanMan,
                    T2.IDPlanPla,
                    T2.Numero
                    FROM Planificacion T0
                    INNER JOIN PlanificacionPlaca T1 ON T0.IDPlan = T1.IDPlan
                    INNER JOIN PlacaManifiesto T2 ON T1.IDPlanPla = T2.IDPlanPla
                    WHERE T2.IDPlanPla = @IDPlan";

                var parameters = new { IDPlan = ID };
                var result = await connection.QueryAsync<ManifiestoPlan>(query, parameters);
                return Ok(result);
            }
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerProductoPlan(int ID) {
            using (var connection = new SqlConnection(_connectionString)) {
                string query = @"
                    SELECT 
                    T3.IDProducto,
                    SUM(T3.Cantidad) AS TotalCantidad,
                    SUBSTRING(T3.Descripcion,0,40) AS Descripcion,
                    T3.MedidaBase,
                    T3.Fabricante,
                    T3.Pesado,
                    T3.SL1Code,
                    T3.SL2Code,
                    T3.SL3Code,
                    T3.SL4Code,
                    (SELECT SUM(P1.Peso*P1.Cantidad) FROM PlacaPedido P1 WHERE P1.IDPlanPla = T1.IDPlanPla AND P1.IDProducto = T3.IDProducto) AS PesoTotal,
                    T1.Placa,
                    T1.Capacidad,
                    T1.Usuario,
                    T3.Linea,
                    SUBSTRING(T3.Fabricante,0,13) AS Fabricante
                    FROM Planificacion T0
                    INNER JOIN PlanificacionPlaca T1 ON T0.IDPlan = T1.IDPlan
                    INNER JOIN PlacaManifiesto T2 ON T1.IDPlanPla = T2.IDPlanPla
                    INNER JOIN PlacaPedido T3 ON T2.IDPlanMan = T3.IDPlanMan
                    LEFT JOIN PickeoProducto T4 ON T4.IDProducto = T3.IDProducto AND T4.IDPlaca = T1.IDPlanPla
                    WHERE 
                    T1.IDPlanPla = @IDPlan
                    AND T4.IDProducto IS NULL
                    GROUP BY 
                    T1.IDPlanPla,
                    T3.IDProducto,
                    T3.Descripcion,
                    T3.MedidaBase,
                    T3.Fabricante,
                    T3.Pesado,
                    T3.SL1Code,
                    T3.SL2Code,
                    T3.SL3Code,
                    T3.SL4Code,
                    T1.Placa,
                    T1.Capacidad,
                    T3.Linea,
                    T3.Fabricante,
                    T1.Usuario
                    ORDER BY Pesado, SL1Code, SL2Code, SL3Code, SL4Code";

                var parameters = new { IDPlan = ID };
                var result = await connection.QueryAsync<ProductoPlan>(query, parameters);
                Console.WriteLine(result);
                return Ok(result);
            }
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerProductoPlanPickeador(int ID, int idpick) {
            using (var connection = new SqlConnection(_connectionString)) {
                string query = @"
                    SELECT 
                    T3.IDProducto,
                    SUM(T3.Cantidad) AS TotalCantidad,
                    SUBSTRING(T3.Descripcion,0,35) AS Descripcion,
                    T3.MedidaBase,
                    T3.Fabricante,
                    T3.Pesado,
                    T3.SL1Code,
                    T3.SL2Code,
                    T3.SL3Code,
                    T3.SL4Code,
                    (SELECT SUM(P1.Peso*P1.Cantidad) FROM PlacaPedido P1 WHERE P1.IDPlanPla = T1.IDPlanPla AND P1.IDProducto = T3.IDProducto) AS PesoTotal,
                    T1.Placa,
                    T1.Capacidad,
                    T5.Nombre
                    FROM Planificacion T0
                    INNER JOIN PlanificacionPlaca T1 ON T0.IDPlan = T1.IDPlan
                    INNER JOIN PlacaManifiesto T2 ON T1.IDPlanPla = T2.IDPlanPla
                    INNER JOIN PlacaPedido T3 ON T2.IDPlanMan = T3.IDPlanMan
                    INNER JOIN PickeoProducto T4 ON T4.IDProducto = T3.IDProducto AND T4.IDPlaca = T1.IDPlanPla
                    INNER JOIN PickeoPersonal T5 ON T4.IDPick = T5.IDPP
                    WHERE 
                    T1.IDPlanPla = @IDPlan
                    AND T4.IDPick = @idpick
                    GROUP BY 
                    T1.IDPlanPla,
                    T3.IDProducto,
                    T3.Descripcion,
                    T3.MedidaBase,
                    T3.Fabricante,
                    T3.Pesado,
                    T3.SL1Code,
                    T3.SL2Code,
                    T3.SL3Code,
                    T3.SL4Code,
                    T1.Placa,
                    T1.Capacidad,
                    T5.Nombre
                    ORDER BY Pesado, SL1Code, SL2Code, SL3Code, SL4Code";

                var parameters = new { IDPlan = ID, idpick };
                var result = await connection.QueryAsync<ProductoPlan>(query, parameters);
                return Ok(result);
            }
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerDataProductoPlan(int ID) {
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
                    FROM Planificacion T0
                    INNER JOIN PlanificacionPlaca T1 ON T0.IDPlan = T1.IDPlan
                    INNER JOIN PlacaManifiesto T2 ON T1.IDPlanPla = T2.IDPlanPla
                    INNER JOIN PlacaPedido T3 ON T2.IDPlanMan = T3.IDPlanMan
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
        public async Task<IActionResult> ObtenerProductoAsignado(int ID) {
            using (var connection = new SqlConnection(_connectionString)) {
                string query = @"
                    SELECT 
                    T3.IDProducto,
                    SUM(T3.Cantidad) AS TotalCantidad,
                    T3.Descripcion,
                    T3.MedidaBase,
                    T3.Fabricante,
                    T5.Nombre
                    FROM Planificacion T0
                    INNER JOIN PlanificacionPlaca T1 ON T0.IDPlan = T1.IDPlan
                    INNER JOIN PlacaManifiesto T2 ON T1.IDPlanPla = T2.IDPlanPla
                    INNER JOIN PlacaPedido T3 ON T2.IDPlanMan = T3.IDPlanMan
                    INNER JOIN PickeoProducto T4 ON T4.IDPlaca = T1.IDPlanPla AND T4.IDProducto = T3.IDProducto
                    INNER JOIN PickeoPersonal T5 ON T5.IDPP = T4.IDPick
                    WHERE 
                    T1.IDPlanPla = @IDPlan
                    GROUP BY 
                    T3.IDProducto,
                    T3.Descripcion,
                    T3.MedidaBase,
                    T3.Fabricante,
                    T5.Nombre";

                var parameters = new { IDPlan = ID };
                var result = await connection.QueryAsync<ProductoAsig>(query, parameters);
                return Ok(result);
            }
        }

        [HttpGet]
        public async Task<IActionResult> ConsultarDetallePickeador(int idPick, int Plan) {
            /////////
            var result2 = "";

            using (var connection1 = new SqlConnection(_connectionString)) {
                string query = @"
                        SELECT T1.Tipo 
                        FROM PlanificacionTraslado T0 
                        INNER JOIN CodigosBeetrack T1 ON T0.IDPlanTraslado = T1.IDPlan 
                        INNER JOIN PlanificacionPlacaTransferencia T2 ON T2.IDPlan = T1.IDPlan 
                        WHERE T2.IDPlanPla = @Plan"
                ;

                var parameters = new { Plan = Plan };
                result2 = await connection1.QueryFirstOrDefaultAsync<string>(query, parameters);

            }
            if (result2 == "Transferencia") {
                using (var connection = new SqlConnection(_connectionString)) {
                    string query = @"
                    SELECT 
                    T0.IDProducto,
                    T0.IDPProducto,
                    T0.Cantidad AS Cantidad, 
                    MAX(T2.Descripcion) AS Descripcion,
                    T2.SL1Code,
                    T2.SL2Code,
                    T2.SL3Code,
                    T2.SL4Code,
                    T2.Fabricante,
                    T2.StockActual AS StockAct,
                    (SELECT SUM(P1.Cantidad*P1.Factor) FROM PickeoProductoIngresado P1 WHERE P1.IDPProducto = T0.IDPProducto) AS CantidadContada,
                    (SELECT COUNT(P2.IDPIngresado) FROM PickeoProductoIngresado P2 WHERE P2.IDPProducto = T0.IDPProducto) AS Iniciado,
                    T0.Finalizado,
                    T0.Reconteo,
                    T0.Aceptado
                    FROM PickeoProducto T0
                    INNER JOIN PickeoPersonal T1 ON T1.IDPP = T0.IDPick
                    LEFT JOIN PlacaPedidoTransferencia T2 ON T2.IDProducto = T0.IDProducto AND T0.IDPlaca = T2.IDPlanPla
                    WHERE 
                    T0.IDPlaca = @Plan
                    AND T0.IDPick = @idPick
                    GROUP BY 
                    T0.IDProducto,
                    T0.IDPProducto,
                    T0.Cantidad,
                    T2.SL1Code,
                    T2.SL2Code,
                    T2.SL3Code,
                    T2.SL4Code,
                    T2.StockActual,
                    T2.Fabricante,
                    T0.Finalizado,
                    T0.Reconteo,
                    T0.Aceptado";

                    var parameters = new { idPick, Plan };
                    var result = await connection.QueryAsync<ProductosContador>(query, parameters);
                    return Ok(result);
                }
            } else {
                using (var connection = new SqlConnection(_connectionString)) {
                    string query = @"
                    SELECT 
                    T0.IDProducto,
                    T0.IDPProducto,
                    T0.Cantidad AS Cantidad, 
                    MAX(T2.Descripcion) AS Descripcion,
                    T2.SL1Code,
                    T2.SL2Code,
                    T2.SL3Code,
                    T2.SL4Code,
                    T2.Fabricante,
                    T2.StockActual AS StockAct,
                    (SELECT SUM(P1.Cantidad*P1.Factor) FROM PickeoProductoIngresado P1 WHERE P1.IDPProducto = T0.IDPProducto) AS CantidadContada,
                    (SELECT COUNT(P2.IDPIngresado) FROM PickeoProductoIngresado P2 WHERE P2.IDPProducto = T0.IDPProducto) AS Iniciado,
                    T0.Finalizado,
                    T0.Reconteo,
                    T0.Aceptado
                    FROM PickeoProducto T0
                    INNER JOIN PickeoPersonal T1 ON T1.IDPP = T0.IDPick
                    LEFT JOIN PlacaPedido T2 ON T2.IDProducto = T0.IDProducto AND T0.IDPlaca = T2.IDPlanPla
                    WHERE 
                    T0.IDPlaca = @Plan
                    AND T0.IDPick = @idPick
                    GROUP BY 
                    T0.IDProducto,
                    T0.IDPProducto,
                    T0.Cantidad,
                    T2.SL1Code,
                    T2.SL2Code,
                    T2.SL3Code,
                    T2.SL4Code,
                    T2.StockActual,
                    T2.Fabricante,
                    T0.Finalizado,
                    T0.Reconteo,
                    T0.Aceptado";

                    var parameters = new { idPick, Plan };
                    var result = await connection.QueryAsync<ProductosContador>(query, parameters);
                    return Ok(result);
                }
            }


        }

        [HttpGet]
        public async Task<IActionResult> ObtenerContadoresAsignados(int ID) {
            using (var connection = new SqlConnection(_connectionString)) {
                string query = @"
                    SELECT 
                    T1.Nombre,
                    T1.IDPP,
                    COUNT(T0.IDPProducto) AS Items,
                    COUNT(T2.IDPProducto) AS Finalizados
                    FROM PickeoProducto T0
                    INNER JOIN PickeoPersonal T1 ON T1.IDPP = T0.IDPick
                    LEFT JOIN PickeoProducto T2 ON T2.IDPick = T0.IDPick AND T2.IDPlaca = T0.IDPlaca AND T2.Finalizado = 1
                    WHERE T0.IDPlaca = @IDPlan
                    GROUP BY T1.Nombre, T1.IDPP
                    ";

                var parameters = new { IDPlan = ID };
                var result = await connection.QueryAsync<ContadorAgrupado>(query, parameters);
                return Ok(result);
            }
        }
        [HttpGet]
        public async Task<IActionResult> ObtenerDataPickeado(int ID) {
            using (var connection = new SqlConnection(_connectionString)) {
                string query = @"
                    SELECT 
                    T1.Nombre,
                    T1.IDPP,
                    T2.IDProducto,
                    T4.Descripcion,
                    T2.IDPProducto,
                    COUNT(T0.IDPProducto) AS Items,
                    COUNT(T2.IDPProducto) AS Finalizados,
                    T2.Cantidad,
                    (SELECT COALESCE(SUM(P1.Cantidad*P1.Factor),0) FROM PickeoProductoIngresado P1 WHERE P1.IDPProducto = T2.IDPProducto) AS CantidadPicada
                    FROM PickeoProducto T0
                    INNER JOIN PickeoPersonal T1 ON T1.IDPP = T0.IDPick
                    LEFT JOIN PickeoProducto T2 ON T2.IDPick = T0.IDPick AND T2.IDPlaca = T0.IDPlaca AND T2.Finalizado = 1
                    LEFT JOIN PlacaPedido T4 ON T2.IDProducto = T4.IDProducto
                    WHERE T0.IDPlaca = @IDPlan
                    GROUP BY 
                    T1.Nombre,
                    T4.Descripcion,
                    T1.IDPP, 
                    T2.IDProducto,
                    T2.IDPProducto,
                    T2.Cantidad
                    ";

                var parameters = new { IDPlan = ID };
                var result = await connection.QueryAsync<DataPickeado>(query, parameters);
                return Ok(result);
            }
        }

        [HttpGet]
        public async Task<IActionResult> FiltroPickador(int idContador, int idPlan) {
            using (var connection = new SqlConnection(_connectionString)) {
                string query = @"
                    SELECT 
                    T2.SL1Code,
                    T2.SL2Code
                    FROM PickeoProducto T0
                    LEFT JOIN PlacaPedido T2 ON T2.IDProducto = T0.IDProducto
                    WHERE 
                    T0.IDPlaca = @idPlan
                    AND T0.IDPick = @idContador
                    GROUP BY 
                    T2.SL1Code,
                    T2.SL2Code
                    ORDER BY T2.SL1Code,T2.SL2Code";

                var parameters = new { idContador, idPlan };
                var result = await connection.QueryAsync<FiltroPickador>(query, parameters);
                return Ok(result);
            }
        }

        [HttpDelete]
        public ActionResult BorrarPicking(int idpick, int plan) {
            try {
                using (var connection = new SqlConnection(_connectionString)) {
                    connection.Open();

                    string deletePedidosQuery = "DELETE FROM PickeoProducto WHERE IDPlaca = @Plan AND IDPick = @IDPick";
                    using (var command = new SqlCommand(deletePedidosQuery, connection)) {
                        command.Parameters.AddWithValue("@Plan", plan);
                        command.Parameters.AddWithValue("@IDPick", idpick);
                        command.ExecuteNonQuery();
                    }
                    return Json(new { success = true, message = "Asignacion borrada correctamente." });
                }
            } catch (Exception ex) {
                return Json(new { success = false, message = "Hubo un error al borrar la subida.", error = ex.Message });
            }
        }

        [HttpPost]
        public ActionResult IniciarPicking(int idpick, int plan) {
            try {
                using (var connection = new SqlConnection(_connectionString)) {
                    connection.Open();

                    string deletePedidosQuery = "UPDATE PickeoProducto SET FechaCre = GETDATE() WHERE IDPlaca = @Plan AND IDPick = @IDPick";
                    using (var command = new SqlCommand(deletePedidosQuery, connection)) {
                        command.Parameters.AddWithValue("@Plan", plan);
                        command.Parameters.AddWithValue("@IDPick", idpick);
                        command.ExecuteNonQuery();
                    }
                    return Json(new { success = true, message = "Picking iniciado correctamente." });
                }
            } catch (Exception ex) {
                return Json(new { success = false, message = "Hubo un error al inciar el picking", error = ex.Message });
            }
        }

        [HttpPost]
        public ActionResult CambiarPicking(int idpick, int plan, int pick) {
            try {
                using (var connection = new SqlConnection(_connectionString)) {
                    connection.Open();

                    string deletePedidosQuery = "UPDATE PickeoProducto SET IDPick = @Pick  WHERE IDPlaca = @Plan AND IDPick = @IDPick";
                    using (var command = new SqlCommand(deletePedidosQuery, connection)) {
                        command.Parameters.AddWithValue("@Pick", pick);
                        command.Parameters.AddWithValue("@Plan", plan);
                        command.Parameters.AddWithValue("@IDPick", idpick);
                        command.ExecuteNonQuery();
                    }
                    return Json(new { success = true, message = "Asignacion cambiada correctamente." });
                }
            } catch (Exception ex) {
                return Json(new { success = false, message = "Hubo un error al cambiar la subida.", error = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerProductosContador(int idContador, int idPlan) {
            ////////
            var result2 = "";

            using (var connection1 = new SqlConnection(_connectionString)) {
                string query = @"
                        SELECT T1.Tipo 
                        FROM PlanificacionTraslado T0 
                        INNER JOIN CodigosBeetrack T1 ON T0.IDPlanTraslado = T1.IDPlan 
                        INNER JOIN PlanificacionPlacaTransferencia T2 ON T2.IDPlan = T1.IDPlan 
                        WHERE T2.IDPlanPla = @idPlan"
                ;

                var parameters = new { idPlan = idPlan };

                result2 = await connection1.QueryFirstOrDefaultAsync<string>(query, parameters);

            }
            if (result2 == "Transferencia") {
                using (var connection = new SqlConnection(_connectionString)) {

                    string almacenQuery = @"
SELECT TOP 1 T0.AlmacenTraslado 
FROM PlanificacionTraslado T0 
INNER JOIN PlanificacionPlacaTransferencia T1 ON T1.IDPlan = T0.IDPlanTraslado 
WHERE IDPlanPla = @idPlan";

                    var almacenParam = new { idPlan };
                    var almacenResult = await connection.QuerySingleOrDefaultAsync<string>(almacenQuery, almacenParam);

                    if (almacenResult == null) {
                        return BadRequest("No se encontró el almacén para el IDPlan proporcionado.");
                    }

                    string almacen = almacenResult;

                    string query = @"
                    SELECT 
                    T0.IDProducto,
                    T0.IDPProducto,
                    T0.Cantidad AS Cantidad, 
                    T0.Finalizado,
                    MAX(T2.Descripcion) AS Descripcion,
                    T2.SL1Code,
                    T2.SL2Code,
                    T2.SL3Code,
                    T2.SL4Code,
                    T2.CodigoFabricante,
                    T2.AbsEntry,
                    T2.Ubicacion,
                    T2.StockActual AS StockGuardado,
                    (SELECT SUM(P1.Cantidad*P1.Factor) FROM PickeoProductoIngresado P1 WHERE P1.IDPProducto = T0.IDPProducto) AS CantidadContada,
                    (SELECT COUNT(P2.IDPIngresado) FROM PickeoProductoIngresado P2 WHERE P2.IDPProducto = T0.IDPProducto) AS Iniciado,
                    T2.MedidaBase
                    FROM PickeoProducto T0
                    INNER JOIN PickeoPersonal T1 ON T1.IDPP = T0.IDPick
                    LEFT JOIN PlacaPedidoTransferencia T2 ON T2.IDProducto = T0.IDProducto AND T2.IDPlanPla = T0.IDPlaca
                    WHERE 
                    T0.IDPlaca = @idPlan
                    AND T0.IDPick = @idContador
                    GROUP BY 
                    T0.IDProducto,
                    T0.IDPProducto,
                    T0.Cantidad,
                    T0.Finalizado,
                    T2.SL1Code,
                    T2.SL2Code,
                    T2.SL3Code,
                    T2.SL4Code,
                    T2.CodigoFabricante,
                    T2.AbsEntry,
                    T2.StockActual,
                    T2.Ubicacion,
                    T2.MedidaBase
                    ORDER BY T2.SL1Code,T2.SL2Code, T2.SL3Code, T2.SL4Code";

                    var parameters = new { idContador, idPlan };
                    var productos = await connection.QueryAsync<ProductosContador>(query, parameters);

                    HanaConnection hanaConnection = new(_hanaConnectionString);

                    try {
                        await hanaConnection.OpenAsync();

                        foreach (var producto in productos) {
                            string hanaQuery = """
                             SELECT T0."OnHand", T1."IWeight1"
                             FROM OITW T0
                             INNER JOIN OITM T1 ON T0."ItemCode" = T1."ItemCode"
                             WHERE T0."ItemCode" = '
                             """
                                 + producto.IDProducto +
                                 """
                             ' 
                             AND T0."WhsCode" = '
                             """
                                 + almacen +
                                 """
                             ' 
                             """;

                            var result = await hanaConnection.QuerySingleOrDefaultAsync<(decimal? OnHand, decimal? IWeight1)>(hanaQuery);

                            if (result.OnHand.HasValue) {
                                producto.StockGuardado = result.OnHand.Value;
                            }

                            if (result.IWeight1.HasValue) {
                                producto.PesoUnidad = result.IWeight1.Value;
                            }
                        }

                        return Ok(productos);
                    } catch (Exception ex) {
                        return BadRequest($"Error al consultar Hana: {ex.Message}");
                    } finally {
                        if (hanaConnection.State == System.Data.ConnectionState.Open) {
                            hanaConnection.Close();
                        }
                    }
                }
            } else {
                using (var connection = new SqlConnection(_connectionString)) {

                    string almacenQuery = @"
                    SELECT TOP 1 T0.Almacen 
                    FROM Planificacion T0 
                    INNER JOIN PlanificacionPlaca T1 ON T1.IDPlan = T0.IDPlan 
                    WHERE IDPlanPla = @idPlan";

                    var almacenParam = new { idPlan };
                    var almacenResult = await connection.QuerySingleOrDefaultAsync<string>(almacenQuery, almacenParam);

                    if (almacenResult == null) {
                        return BadRequest("No se encontró el almacén para el IDPlan proporcionado.");
                    }

                    string almacen = almacenResult;

                    string query = @"
                    SELECT 
                    T0.IDProducto,
                    T0.IDPProducto,
                    T0.Cantidad AS Cantidad, 
                    T0.Finalizado,
                    MAX(T2.Descripcion) AS Descripcion,
                    T2.SL1Code,
                    T2.SL2Code,
                    T2.SL3Code,
                    T2.SL4Code,
                    T2.CodigoFabricante,
                    T2.AbsEntry,
                    T2.Ubicacion,
                    T2.StockActual AS StockGuardado,
                    (SELECT SUM(P1.Cantidad*P1.Factor) FROM PickeoProductoIngresado P1 WHERE P1.IDPProducto = T0.IDPProducto) AS CantidadContada,
                    (SELECT COUNT(P2.IDPIngresado) FROM PickeoProductoIngresado P2 WHERE P2.IDPProducto = T0.IDPProducto) AS Iniciado,
                    T2.MedidaBase
                    FROM PickeoProducto T0
                    INNER JOIN PickeoPersonal T1 ON T1.IDPP = T0.IDPick
                    LEFT JOIN PlacaPedido T2 ON T2.IDProducto = T0.IDProducto AND T2.IDPlanPla = T0.IDPlaca
                    WHERE 
                    T0.IDPlaca = @idPlan
                    AND T0.IDPick = @idContador
                    GROUP BY 
                    T0.IDProducto,
                    T0.IDPProducto,
                    T0.Cantidad,
                    T0.Finalizado,
                    T2.SL1Code,
                    T2.SL2Code,
                    T2.SL3Code,
                    T2.SL4Code,
                    T2.CodigoFabricante,
                    T2.AbsEntry,
                    T2.StockActual,
                    T2.Ubicacion,
                    T2.MedidaBase
                    ORDER BY T2.SL1Code,T2.SL2Code, T2.SL3Code, T2.SL4Code";

                    var parameters = new { idContador, idPlan };
                    var productos = await connection.QueryAsync<ProductosContador>(query, parameters);

                    HanaConnection hanaConnection = new(_hanaConnectionString);

                    try {
                        await hanaConnection.OpenAsync();

                        foreach (var producto in productos) {
                            string hanaQuery = """
                             SELECT T0."OnHand", T1."IWeight1"
                             FROM OITW T0
                             INNER JOIN OITM T1 ON T0."ItemCode" = T1."ItemCode"
                             WHERE T0."ItemCode" = '
                             """
                                 + producto.IDProducto +
                                 """
                             ' 
                             AND T0."WhsCode" = '
                             """
                                 + almacen +
                                 """
                             ' 
                             """;

                            var result = await hanaConnection.QuerySingleOrDefaultAsync<(decimal? OnHand, decimal? IWeight1)>(hanaQuery);

                            if (result.OnHand.HasValue) {
                                producto.StockGuardado = result.OnHand.Value;
                            }

                            if (result.IWeight1.HasValue) {
                                producto.PesoUnidad = result.IWeight1.Value;
                            }
                        }

                        return Ok(productos);
                    } catch (Exception ex) {
                        return BadRequest($"Error al consultar Hana: {ex.Message}");
                    } finally {
                        if (hanaConnection.State == System.Data.ConnectionState.Open) {
                            hanaConnection.Close();
                        }
                    }
                }
            }
        }



        [HttpGet]
        public async Task<IActionResult> ObtenerProductosVerificar(int idPlan) {
            using (var connection = new SqlConnection(_connectionString)) {
                string query = @"
                    SELECT 
                        T0.IDProducto,
                        T0.IDPProducto,
                        T0.Reconteo,
                        T0.Cantidad AS Cantidad, 
                        COALESCE(T0.Verificado,0) AS Verificado,
                        T0.Finalizado,
	                    T0.Aceptado,
                        MAX(T2.Descripcion) AS Descripcion,
                        T2.SL1Code,
                        T2.SL2Code,
                        T2.SL3Code,
                        T2.SL4Code,
                        T2.CodigoFabricante,
	                    T2.Fabricante,
                        T2.AbsEntry,
                        (SELECT SUM(P1.Cantidad*P1.Factor) FROM PickeoProductoIngresado P1 WHERE P1.IDPProducto = T0.IDPProducto) AS CantidadContada,
                        (SELECT COUNT(P2.IDPIngresado) FROM PickeoProductoIngresado P2 WHERE P2.IDPProducto = T0.IDPProducto) AS Iniciado,
                        COALESCE(T3.Factor,0) AS Factor,
                        COALESCE(T3.Medidad, 'NA') AS Medidad,
                        (SELECT COALESCE(SUM(P3.Cantidad),0) FROM PickeoProductoIngresado P3 WHERE P3.IDPProducto = T3.IDPProducto AND P3.Factor = T3.Factor) AS CantidadPaquete
                    FROM PickeoProducto T0
                    INNER JOIN PickeoPersonal T1 ON T1.IDPP = T0.IDPick
                    LEFT JOIN PlacaPedido T2 ON T2.IDProducto = T0.IDProducto
                    LEFT JOIN PickeoProductoIngresado T3 ON T0.IDPProducto = T3.IDPProducto AND T3.Cantidad != 0
                    WHERE 
                        T0.IDPlaca = @idPlan
                    GROUP BY 
                        T0.IDProducto,
                        T0.IDPProducto,
                        T0.Reconteo,
                        T0.Cantidad,
                        T0.Verificado,
                        T0.Finalizado,
                        T0.Aceptado,
                        T2.SL1Code,
                        T2.SL2Code,
                        T2.SL3Code,
                        T2.SL4Code,
                        T2.CodigoFabricante,
	                    T2.Fabricante,
                        T2.AbsEntry,
                        T3.Factor,
                        T3.Medidad,
                        T3.IDPProducto
	                    ";

                var parameters = new { idPlan };
                var result = await connection.QueryAsync<ProductosContador, PaqueteInfo, ProductosContador>(
                    query,
                    (productosContador, paqueteInfo) => {
                        productosContador.Paquetes.Add(paqueteInfo);
                        return productosContador;
                    },
                    parameters,
                    splitOn: "CantidadPaquete,Factor"
                );

                var groupedResult = result
                    .GroupBy(p => p.IDProducto)
                    .Select(g => new ProductosContador {
                        IDProducto = g.Key,
                        IDPProducto = g.FirstOrDefault()?.IDPProducto,
                        Cantidad = g.FirstOrDefault()?.Cantidad ?? 0,
                        Finalizado = g.FirstOrDefault()?.Finalizado ?? 0,
                        Aceptado = g.FirstOrDefault()?.Aceptado ?? 0,
                        Cargado = g.FirstOrDefault()?.Cargado ?? 0,
                        Verificado = g.FirstOrDefault()?.Verificado ?? 0,
                        CantidadContada = g.FirstOrDefault()?.CantidadContada ?? 0,
                        Descripcion = g.FirstOrDefault()?.Descripcion,
                        Ubicacion = g.FirstOrDefault()?.Ubicacion,
                        SL1Code = g.FirstOrDefault()?.SL1Code,
                        SL2Code = g.FirstOrDefault()?.SL2Code,
                        SL3Code = g.FirstOrDefault()?.SL3Code,
                        SL4Code = g.FirstOrDefault()?.SL4Code,
                        Fabricante = g.FirstOrDefault()?.Fabricante,
                        Iniciado = g.FirstOrDefault()?.Iniciado,
                        Estado = g.FirstOrDefault()?.Estado ?? 0,
                        CodigoFabricante = g.FirstOrDefault()?.CodigoFabricante,
                        AbsEntry = g.FirstOrDefault()?.AbsEntry ?? 0,
                        Reconteo = g.FirstOrDefault()?.Reconteo ?? 0,
                        Paquetes = g.SelectMany(p => p.Paquetes).ToList()
                    })
                    .ToList();

                return Ok(groupedResult);
            }
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerProductosVerificarPickador(int idPlan, int idpick) {
            var result2 = "";

            using (var connection1 = new SqlConnection(_connectionString)) {
                string query = @"
                        SELECT T1.Tipo 
                        FROM PlanificacionTraslado T0 
                        INNER JOIN CodigosBeetrack T1 ON T0.IDPlanTraslado = T1.IDPlan 
                        INNER JOIN PlanificacionPlacaTransferencia T2 ON T2.IDPlan = T1.IDPlan 
                        WHERE T2.IDPlanPla = @idPlan"
                ;

                var parameters = new { idPlan = idPlan };

                result2 = await connection1.QueryFirstOrDefaultAsync<string>(query, parameters);

            }

            if (result2 == "Transferencia") {
                using (var connection = new SqlConnection(_connectionString)) {
                    string query = @"
                    SELECT 
                        T0.IDProducto,
                        T0.IDPProducto,
                        T0.Reconteo,
                        T0.Cantidad AS Cantidad, 
                        T0.Marcado,
                        COALESCE(T0.Verificado,0) AS Verificado,
                        T0.Finalizado,
                        T0.Aceptado,
                        MAX(T2.Descripcion) AS Descripcion,
                        T2.SL1Code,
                        T2.SL2Code,
                        T2.SL3Code,
                        T2.SL4Code,
                        T2.CodigoFabricante,
                        T2.Fabricante,
                        T2.AbsEntry,
                        T2.CodigoBarras,
	                    T2.MedidaBase,
                        (SELECT SUM(P1.Cantidad*P1.Factor) FROM PickeoProductoIngresado P1 WHERE P1.IDPProducto = T0.IDPProducto) AS CantidadContada,
                        (SELECT COUNT(P2.IDPIngresado) FROM PickeoProductoIngresado P2 WHERE P2.IDPProducto = T0.IDPProducto) AS Iniciado,
                        COALESCE(T3.Factor,0) AS Factor,
                        COALESCE(T3.Medidad, 'NA') AS Medidad,
                        (SELECT COALESCE(SUM(P3.Cantidad),0) FROM PickeoProductoIngresado P3 WHERE P3.IDPProducto = T3.IDPProducto AND P3.Factor = T3.Factor) AS CantidadPaquete
                    FROM PickeoProducto T0
                    INNER JOIN PickeoPersonal T1 ON T1.IDPP = T0.IDPick
                    LEFT JOIN PlacaPedidoTransferencia T2 ON T2.IDProducto = T0.IDProducto AND T0.IDPlaca = T2.IDPlanPla
                    LEFT JOIN PickeoProductoIngresado T3 ON T0.IDPProducto = T3.IDPProducto AND T3.Cantidad != 0
                    WHERE 
                        T0.IDPlaca = @idPlan
                        AND T0.IDPick = @idpick
                    GROUP BY 
                        T0.IDProducto,
                        T0.IDPProducto,
                        T0.Reconteo,
                        T0.Cantidad,
                        T0.Marcado,
                        T0.Verificado,
                        T0.Finalizado,
                        T0.Aceptado,
                        T2.SL1Code,
                        T2.SL2Code,
                        T2.SL3Code,
                        T2.SL4Code,
                        T2.CodigoFabricante,
                        T2.Fabricante,
                        T2.AbsEntry,
	                    T2.MedidaBase,
                        T2.CodigoBarras,
                        T3.Factor,
                        T3.Medidad,
                        T3.IDPProducto
	                    ";

                    var parameters = new { idPlan, idpick };
                    var result = await connection.QueryAsync<ProductosContador, PaqueteInfo, ProductosContador>(
                        query,
                        (productosContador, paqueteInfo) => {
                            productosContador.Paquetes.Add(paqueteInfo);
                            return productosContador;
                        },
                        parameters,
                        splitOn: "CantidadPaquete,Factor"
                    );

                    var groupedResult = result
                        .GroupBy(p => p.IDProducto)
                        .Select(g => new ProductosContador {
                            IDProducto = g.Key,
                            IDPProducto = g.FirstOrDefault()?.IDPProducto,
                            Cantidad = g.FirstOrDefault()?.Cantidad ?? 0,
                            Marcado = g.FirstOrDefault()?.Marcado ?? 0,
                            Finalizado = g.FirstOrDefault()?.Finalizado ?? 0,
                            Aceptado = g.FirstOrDefault()?.Aceptado ?? 0,
                            Cargado = g.FirstOrDefault()?.Cargado ?? 0,
                            Verificado = g.FirstOrDefault()?.Verificado ?? 0,
                            CantidadContada = g.FirstOrDefault()?.CantidadContada ?? 0,
                            Descripcion = g.FirstOrDefault()?.Descripcion,
                            Ubicacion = g.FirstOrDefault()?.Ubicacion,
                            SL1Code = g.FirstOrDefault()?.SL1Code,
                            SL2Code = g.FirstOrDefault()?.SL2Code,
                            SL3Code = g.FirstOrDefault()?.SL3Code,
                            SL4Code = g.FirstOrDefault()?.SL4Code,
                            MedidaBase = g.FirstOrDefault()?.MedidaBase,
                            Fabricante = g.FirstOrDefault()?.Fabricante,
                            Iniciado = g.FirstOrDefault()?.Iniciado,
                            Estado = g.FirstOrDefault()?.Estado ?? 0,
                            CodigoFabricante = g.FirstOrDefault()?.CodigoFabricante,
                            AbsEntry = g.FirstOrDefault()?.AbsEntry ?? 0,
                            CodigoBarras = g.FirstOrDefault()?.CodigoBarras,
                            Reconteo = g.FirstOrDefault()?.Reconteo ?? 0,
                            Paquetes = g.SelectMany(p => p.Paquetes).ToList()
                        })
                        .ToList();

                    return Ok(groupedResult);    //Verificar Nombre, Marca, Fabricante, Factor 
                }
            } else {
                using (var connection = new SqlConnection(_connectionString)) {
                    string query = @"
                    SELECT 
                        T0.IDProducto,
                        T0.IDPProducto,
                        T0.Reconteo,
                        T0.Cantidad AS Cantidad, 
                        T0.Marcado,
                        COALESCE(T0.Verificado,0) AS Verificado,
                        T0.Finalizado,
                        T0.Aceptado,
                        MAX(T2.Descripcion) AS Descripcion,
                        T2.SL1Code,
                        T2.SL2Code,
                        T2.SL3Code,
                        T2.SL4Code,
                        T2.CodigoFabricante,
                        T2.Fabricante,
                        T2.AbsEntry,
                        T2.CodigoBarras,
	                    T2.MedidaBase,
                        (SELECT SUM(P1.Cantidad*P1.Factor) FROM PickeoProductoIngresado P1 WHERE P1.IDPProducto = T0.IDPProducto) AS CantidadContada,
                        (SELECT COUNT(P2.IDPIngresado) FROM PickeoProductoIngresado P2 WHERE P2.IDPProducto = T0.IDPProducto) AS Iniciado,
                        COALESCE(T3.Factor,0) AS Factor,
                        COALESCE(T3.Medidad, 'NA') AS Medidad,
                        (SELECT COALESCE(SUM(P3.Cantidad),0) FROM PickeoProductoIngresado P3 WHERE P3.IDPProducto = T3.IDPProducto AND P3.Factor = T3.Factor) AS CantidadPaquete
                    FROM PickeoProducto T0
                    INNER JOIN PickeoPersonal T1 ON T1.IDPP = T0.IDPick
                    LEFT JOIN PlacaPedido T2 ON T2.IDProducto = T0.IDProducto AND T0.IDPlaca = T2.IDPlanPla
                    LEFT JOIN PickeoProductoIngresado T3 ON T0.IDPProducto = T3.IDPProducto AND T3.Cantidad != 0
                    WHERE 
                        T0.IDPlaca = @idPlan
                        AND T0.IDPick = @idpick
                    GROUP BY 
                        T0.IDProducto,
                        T0.IDPProducto,
                        T0.Reconteo,
                        T0.Cantidad,
                        T0.Marcado,
                        T0.Verificado,
                        T0.Finalizado,
                        T0.Aceptado,
                        T2.SL1Code,
                        T2.SL2Code,
                        T2.SL3Code,
                        T2.SL4Code,
                        T2.CodigoFabricante,
                        T2.Fabricante,
                        T2.AbsEntry,
	                    T2.MedidaBase,
                        T2.CodigoBarras,
                        T3.Factor,
                        T3.Medidad,
                        T3.IDPProducto
	                    ";

                    var parameters = new { idPlan, idpick };
                    var result = await connection.QueryAsync<ProductosContador, PaqueteInfo, ProductosContador>(
                        query,
                        (productosContador, paqueteInfo) => {
                            productosContador.Paquetes.Add(paqueteInfo);
                            return productosContador;
                        },
                        parameters,
                        splitOn: "CantidadPaquete,Factor"
                    );

                    var groupedResult = result
                        .GroupBy(p => p.IDProducto)
                        .Select(g => new ProductosContador {
                            IDProducto = g.Key,
                            IDPProducto = g.FirstOrDefault()?.IDPProducto,
                            Cantidad = g.FirstOrDefault()?.Cantidad ?? 0,
                            Marcado = g.FirstOrDefault()?.Marcado ?? 0,
                            Finalizado = g.FirstOrDefault()?.Finalizado ?? 0,
                            Aceptado = g.FirstOrDefault()?.Aceptado ?? 0,
                            Cargado = g.FirstOrDefault()?.Cargado ?? 0,
                            Verificado = g.FirstOrDefault()?.Verificado ?? 0,
                            CantidadContada = g.FirstOrDefault()?.CantidadContada ?? 0,
                            Descripcion = g.FirstOrDefault()?.Descripcion,
                            Ubicacion = g.FirstOrDefault()?.Ubicacion,
                            SL1Code = g.FirstOrDefault()?.SL1Code,
                            SL2Code = g.FirstOrDefault()?.SL2Code,
                            SL3Code = g.FirstOrDefault()?.SL3Code,
                            SL4Code = g.FirstOrDefault()?.SL4Code,
                            MedidaBase = g.FirstOrDefault()?.MedidaBase,
                            Fabricante = g.FirstOrDefault()?.Fabricante,
                            Iniciado = g.FirstOrDefault()?.Iniciado,
                            Estado = g.FirstOrDefault()?.Estado ?? 0,
                            CodigoFabricante = g.FirstOrDefault()?.CodigoFabricante,
                            AbsEntry = g.FirstOrDefault()?.AbsEntry ?? 0,
                            CodigoBarras = g.FirstOrDefault()?.CodigoBarras,
                            Reconteo = g.FirstOrDefault()?.Reconteo ?? 0,
                            Paquetes = g.SelectMany(p => p.Paquetes).ToList()
                        })
                        .ToList();

                    return Ok(groupedResult);
                }
            }




        }

        [HttpGet]
        public async Task<IActionResult> ObtenerDataExportar(int idpick, int idplan) {
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
	                    T6.Nombre AS Jefe,
	                    (SELECT COUNT(P1.IDPProducto) FROM PickeoProducto P1 WHERE P1.IDPlaca = T1.IDPlanPla AND P1.IDPick = T4.IDPick) AS TotalItems
                        FROM Planificacion T0
                        INNER JOIN PlanificacionPlaca T1 ON T0.IDPlan = T1.IDPlan
                        INNER JOIN PlacaManifiesto T2 ON T1.IDPlanPla = T2.IDPlanPla
                        INNER JOIN PlacaPedido T3 ON T2.IDPlanMan = T3.IDPlanMan
                        INNER JOIN PickeoProducto T4 ON T4.IDProducto = T3.IDProducto AND T4.IDPlaca = T1.IDPlanPla
	                    INNER JOIN PickeoPersonal T5 ON T5.IDPP = T4.IDPick
	                    INNER JOIN PickeoPersonal T6 ON T6.IDPP = T1.Usuario
                        WHERE 
                        T1.IDPlanPla = @IDPlan
                        AND T4.IDPick = @IDPick
                        GROUP BY 
                        T1.IDPlanPla,
	                    T1.Placa,
                        T1.Capacidad,
                        T6.Nombre,
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
	                    T4.IDPick
                        ORDER BY Pesado, SL1Code, SL2Code, SL3Code, SL4Code";

                var parameters = new { IDPlan = idplan, IDPick = idpick };
                var result = await connection.QueryAsync<DataExportarPick>(query, parameters);
                return Ok(result);
            }
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerDataExportarPlan(int idpick, int idplan) {
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
	                    (SELECT COUNT(P1.IDPProducto) FROM PickeoProducto P1 WHERE P1.IDPlaca = T1.IDPlanPla AND P1.IDPick = T4.IDPick) AS TotalItems
                        FROM Planificacion T0
                        INNER JOIN PlanificacionPlaca T1 ON T0.IDPlan = T1.IDPlan
                        INNER JOIN PlacaManifiesto T2 ON T1.IDPlanPla = T2.IDPlanPla
                        INNER JOIN PlacaPedido T3 ON T2.IDPlanMan = T3.IDPlanMan
                        INNER JOIN PickeoProducto T4 ON T4.IDProducto = T3.IDProducto AND T4.IDPlaca = T1.IDPlanPla
	                    INNER JOIN PickeoPersonal T5 ON T5.IDPP = T4.IDPick
                        WHERE 
                        T1.IDPlanPla = @IDPlan
                        AND T4.IDPick = @IDPick
                        GROUP BY 
                        T1.IDPlanPla,
	                    T1.Placa,
                        T1.Capacidad,
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
	                    T4.IDPick
                        ORDER BY Pesado, SL1Code, SL2Code, SL3Code, SL4Code";

                var parameters = new { IDPlan = idplan, IDPick = idpick };
                var result = await connection.QueryAsync<DataExportarPick>(query, parameters);
                return Ok(result);
            }
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerPickeadorPlacas(int idpp) {
            var result2 = "";

            using (var connection1 = new SqlConnection(_connectionString)) {
                string query = @"
                        SELECT T1.Tipo 
                        FROM PlanificacionTraslado T0 
                        INNER JOIN CodigosBeetrack T1 ON T0.IDPlanTraslado = T1.IDPlan 
                        INNER JOIN PlanificacionPlacaTransferencia T2 ON T2.IDPlan = T1.IDPlan 
                        WHERE T2.IDPlanPla = @idpp"
                ;

                var parameters = new { idpp = idpp };

                result2 = await connection1.QueryFirstOrDefaultAsync<string>(query, parameters);

            }
            if (result2 == "Transferencia") {
                using (var connection = new SqlConnection(_connectionString)) {
                    string query = @"
                    SELECT 
                        T1.Placa, 
                        T1.IDPlanPla,
                        T1.Usuario,
                        MAX(CAST(T0.Finalizado AS INT)) AS MaxFinalizado,
                        MIN(CAST(T0.Finalizado AS INT)) AS MinFinalizado,

                        (SELECT MIN(P1.FechaCre) FROM PickeoProducto P1 WHERE P1.IDPlaca = T1.IDPlanPla AND P1.IDPick = T0.IDPick) AS FechaInicio       
                    FROM PickeoProducto T0 
                    INNER JOIN PlanificacionPlacaTransferencia T1 ON T1.IDPlanPla = T0.IDPlaca 
                    WHERE 
                        T0.IDPick = @IDPlaca and (T0.Finalizado is NULL or T0.Reconteo is not null) 
                    GROUP BY
                        T1.Placa, 
                        T1.IDPlanPla,
                        T0.IDPick,
                    T1.Usuario";

                    var parameters = new { IDPlaca = idpp };
                    var result = await connection.QueryAsync<PlacaPlan>(query, parameters);
                    return Ok(result);
                }
            } else {
                using (var connection = new SqlConnection(_connectionString)) {
                    string query = @"
                    SELECT 
                        T1.Placa, 
                        T1.IDPlanPla,
                        T1.Usuario,
                        MAX(CAST(T0.Finalizado AS INT)) AS MaxFinalizado,
                        MIN(CAST(T0.Finalizado AS INT)) AS MinFinalizado,

                        (SELECT MIN(P1.FechaCre) FROM PickeoProducto P1 WHERE P1.IDPlaca = T1.IDPlanPla AND P1.IDPick = T0.IDPick) AS FechaInicio       
                    FROM PickeoProducto T0 
                    INNER JOIN PlanificacionPlaca T1 ON T1.IDPlanPla = T0.IDPlaca 
                    WHERE 
                        T0.IDPick = @IDPlaca and (T0.Finalizado is NULL or T0.Reconteo is not null) 
                    GROUP BY
                        T1.Placa, 
                        T1.IDPlanPla,
                        T0.IDPick,
                    T1.Usuario";

                    var parameters = new { IDPlaca = idpp };
                    var result = await connection.QueryAsync<PlacaPlan>(query, parameters);
                    return Ok(result);
                }

                
            }
        }
        [HttpGet]
        public async Task<IActionResult> ObtenerPickeadorPlacasTransferencias(int idpp) {
            using (var connection = new SqlConnection(_connectionString)) {
                string query = @"
                    SELECT 
                        T1.Placa, 
                        T1.IDPlanPla,
                        T1.Usuario,
                        MAX(CAST(T0.Finalizado AS INT)) AS MaxFinalizado,
                        MIN(CAST(T0.Finalizado AS INT)) AS MinFinalizado,

                        (SELECT MIN(P1.FechaCre) FROM PickeoProducto P1 WHERE P1.IDPlaca = T1.IDPlanPla AND P1.IDPick = T0.IDPick) AS FechaInicio       
                    FROM PickeoProducto T0 
                    INNER JOIN PlanificacionPlacaTransferencia T1 ON T1.IDPlanPla = T0.IDPlaca 
                    WHERE 
                        T0.IDPick = @IDPlaca and (T0.Finalizado is NULL or T0.Reconteo is not null) 
                    GROUP BY
                        T1.Placa, 
                        T1.IDPlanPla,
                        T0.IDPick,
                    T1.Usuario";

                var parameters = new { IDPlaca = idpp };
                var result = await connection.QueryAsync<PlacaPlan>(query, parameters);
                return Ok(result);
            }
        }





        [HttpGet]
        public async Task<IActionResult> GuardarTodoPicking(int id, int plan) {
            try {
                var result = "";

                using (var connection1 = new SqlConnection(_connectionString)) {
                    string query = @"
                        SELECT T1.Tipo 
                        FROM PlanificacionTraslado T0 
                        INNER JOIN CodigosBeetrack T1 ON T0.IDPlanTraslado = T1.IDPlan 
                        INNER JOIN PlanificacionPlacaTransferencia T2 ON T2.IDPlan = T1.IDPlan 
                        WHERE T2.IDPlanPla = @plan";

                    var parameters = new { plan = plan };

                    result = await connection1.QueryFirstOrDefaultAsync<string>(query, parameters);

                }


                if (result == "Transferencia") {
                    List<ProductoDetallesDto> productosDetalles = new List<ProductoDetallesDto>();

                    string sqlQuery = @"
                    SELECT 
    T3.IDProducto,
    SUM(T3.Cantidad) AS TotalCantidad,
    T3.Descripcion,
    T3.MedidaBase,
    T3.Fabricante,
    T3.CodigoFabricante,
    T3.AbsEntry,
    T3.Pesado,
    T3.SL1Code,
    T3.SL2Code,
    T3.SL3Code,
    T3.SL4Code,
    (SELECT SUM(P1.Peso*P1.Cantidad) FROM PlacaPedido P1 WHERE P1.IDPlanPla = T1.IDPlanPla AND P1.IDProducto = T3.IDProducto) AS PesoTotal,
    T3.Ubicacion,
    T4.IDPProducto
FROM PlanificacionTraslado T0
INNER JOIN PlanificacionPlacaTransferencia T1 ON T0.IDPlanTraslado = T1.IDPlan
INNER JOIN PlacaManifiestoTransferencia T2 ON T1.IDPlanPla = T2.IDPlanPla
INNER JOIN PlacaPedidoTransferencia T3 ON T2.IDPlanMan = T3.IDPlanMan
LEFT JOIN PickeoProducto T4 ON T4.IDProducto = T3.IDProducto AND T4.IDPlaca = T1.IDPlanPla
LEFT JOIN PickeoProductoIngresado T5 ON T5.IDPProducto = T4.IDPProducto
WHERE 
                        T1.IDPlanPla = @plan
                        AND T4.IDPick = @id
                        AND T5.IDPProducto IS NULL
                    GROUP BY 
                        T1.IDPlanPla,
                        T3.IDProducto,
                        T3.Descripcion,
                        T3.MedidaBase,
                        T3.CodigoFabricante,
                        T3.AbsEntry,
                        T3.Fabricante,
                        T3.Pesado,
                        T3.SL1Code,
                        T3.SL2Code,
                        T3.SL3Code,
                        T3.SL4Code,
                        T3.Ubicacion,
                        T4.IDPProducto
                    ORDER BY Pesado, SL1Code, SL2Code, SL3Code, SL4Code";

                    using (var connection = new SqlConnection(_connectionString)) {
                        await connection.OpenAsync();

                        using (var command = new SqlCommand(sqlQuery, connection)) {
                            command.Parameters.AddWithValue("@id", id);
                            command.Parameters.AddWithValue("@plan", plan);

                            using (var reader = await command.ExecuteReaderAsync()) {
                                while (await reader.ReadAsync()) {
                                    var producto = new ProductoDetallesDto {
                                        IDProducto = reader.IsDBNull(reader.GetOrdinal("IDProducto")) ? null : reader.GetString(reader.GetOrdinal("IDProducto")),
                                        TotalCantidad = reader.IsDBNull(reader.GetOrdinal("TotalCantidad")) ? 0 : reader.GetDecimal(reader.GetOrdinal("TotalCantidad")),
                                        Descripcion = reader.IsDBNull(reader.GetOrdinal("Descripcion")) ? null : reader.GetString(reader.GetOrdinal("Descripcion")),
                                        MedidaBase = reader.IsDBNull(reader.GetOrdinal("MedidaBase")) ? null : reader.GetString(reader.GetOrdinal("MedidaBase")),
                                        Fabricante = reader.IsDBNull(reader.GetOrdinal("Fabricante")) ? null : reader.GetString(reader.GetOrdinal("Fabricante")),
                                        CodigoFabricante = reader.IsDBNull(reader.GetOrdinal("CodigoFabricante")) ? null : reader.GetString(reader.GetOrdinal("CodigoFabricante")),
                                        AbsEntry = reader.IsDBNull(reader.GetOrdinal("AbsEntry")) ? null : reader.GetString(reader.GetOrdinal("AbsEntry")),
                                        Pesado = reader.IsDBNull(reader.GetOrdinal("Pesado")) ? 0 : (reader.GetBoolean(reader.GetOrdinal("Pesado")) ? 1 : 0),
                                        SL1Code = reader.IsDBNull(reader.GetOrdinal("SL1Code")) ? null : reader.GetString(reader.GetOrdinal("SL1Code")),
                                        SL2Code = reader.IsDBNull(reader.GetOrdinal("SL2Code")) ? null : reader.GetString(reader.GetOrdinal("SL2Code")),
                                        SL3Code = reader.IsDBNull(reader.GetOrdinal("SL3Code")) ? null : reader.GetString(reader.GetOrdinal("SL3Code")),
                                        SL4Code = reader.IsDBNull(reader.GetOrdinal("SL4Code")) ? null : reader.GetString(reader.GetOrdinal("SL4Code")),
                                        PesoTotal = reader.IsDBNull(reader.GetOrdinal("PesoTotal")) ? 0 : reader.GetDecimal(reader.GetOrdinal("PesoTotal")),
                                        Ubicacion = reader.IsDBNull(reader.GetOrdinal("Ubicacion")) ? null : reader.GetString(reader.GetOrdinal("Ubicacion")),
                                        IDPProducto = reader.IsDBNull(reader.GetOrdinal("IDPProducto")) ? 0 : reader.GetInt32(reader.GetOrdinal("IDPProducto"))
                                    };


                                    using (var hanaConnection = new HanaConnection(_hanaConnectionString)) {
                                        await hanaConnection.OpenAsync();

                                        string sapQuery = $@"
                                    SELECT DISTINCT T1.""UomName"" AS Medida, T1.""UomCode"" AS IdMedida, 1.0 AS Factor, T2.""OnHand"" AS Stock
                                    FROM ITM1 T0
                                    INNER JOIN OUOM T1 ON T1.""UomEntry"" = T0.""UomEntry""
                                    INNER JOIN OITW T2 ON T2.""ItemCode"" = T0.""ItemCode"" AND T2.""WhsCode"" = 'CENTRAL'
                                    WHERE T0.""ItemCode"" = '{producto.IDProducto}'
                                    UNION ALL
                                    SELECT DISTINCT T1.""UomName"" AS Medida, T1.""UomCode"" AS IdMedida, T2.""BaseQty"" AS Factor, T3.""OnHand"" AS Stock
                                    FROM ITM9 T0
                                    INNER JOIN OUOM T1 ON T1.""UomEntry"" = T0.""UomEntry""
                                    INNER JOIN UGP1 T2 ON T2.""UomEntry"" = T1.""UomEntry""
                                    INNER JOIN OITW T3 ON T3.""ItemCode"" = T0.""ItemCode"" AND T3.""WhsCode"" = 'CENTRAL'
                                    WHERE T0.""ItemCode"" = '{producto.IDProducto}'
                                    ORDER BY Factor";

                                        using (var hanaCommand = new HanaCommand(sapQuery, hanaConnection))
                                        using (var hanaReader = await hanaCommand.ExecuteReaderAsync()) {
                                            while (await hanaReader.ReadAsync()) {
                                                var medida = new MedidaProductoDto {
                                                    Medida = hanaReader.GetString(hanaReader.GetOrdinal("Medida")),
                                                    IdMedida = hanaReader.GetString(hanaReader.GetOrdinal("IdMedida")),
                                                    Factor = hanaReader.GetDecimal(hanaReader.GetOrdinal("Factor")),
                                                    Stock = hanaReader.GetDecimal(hanaReader.GetOrdinal("Stock"))
                                                };


                                                using (var sqlConnection = new SqlConnection(_connectionString)) {
                                                    await sqlConnection.OpenAsync();

                                                    string sqlsubQuery = @"
                                                    SELECT SUM(Cantidad) AS CantidadPedida 
                                                    FROM PlacaPedidoTransferencia 
                                                    WHERE IDProducto = @idProducto 
                                                    AND IDPlanPla = @Plan 
                                                    AND Factor = @factor";

                                                    using (var sqlCommand = new SqlCommand(sqlsubQuery, sqlConnection)) {
                                                        sqlCommand.Parameters.AddWithValue("@idProducto", producto.IDProducto);
                                                        sqlCommand.Parameters.AddWithValue("@Plan", plan);
                                                        sqlCommand.Parameters.AddWithValue("@factor", medida.Factor);

                                                        var cantidadPedida = await sqlCommand.ExecuteScalarAsync();
                                                        medida.CantidadPedida = cantidadPedida != DBNull.Value ? Convert.ToDecimal(cantidadPedida) / medida.Factor : 0;
                                                    }
                                                    producto.Medidas.Add(medida);
                                                }
                                            }
                                        }
                                    }

                                    productosDetalles.Add(producto);
                                }
                            }
                        }
                    }

                    using (var sqlConnection = new SqlConnection(_connectionString)) {
                        await sqlConnection.OpenAsync();

                        foreach (var producto in productosDetalles) {
                            foreach (var medida in producto.Medidas) {
                                string insertQuery = @"
                                INSERT INTO PickeoProductoIngresado (IDPProducto, Factor, Medidad, Cantidad, FechaCre, AbsEntry, UbiAbs, StockGuardado)
                                VALUES (@IDPProducto, @Factor, @Medidad, @Cantidad, GETDATE(), @AbsEntry, @UbiAbs, @StockGuardado)";

                                using (var sqlCommand = new SqlCommand(insertQuery, sqlConnection)) {
                                    sqlCommand.Parameters.AddWithValue("@IDPProducto", producto.IDPProducto);
                                    sqlCommand.Parameters.AddWithValue("@Factor", medida.Factor);
                                    sqlCommand.Parameters.AddWithValue("@Medidad", medida.Medida);
                                    sqlCommand.Parameters.AddWithValue("@Cantidad", medida.CantidadPedida);
                                    sqlCommand.Parameters.AddWithValue("@AbsEntry", producto.AbsEntry);
                                    sqlCommand.Parameters.AddWithValue("@UbiAbs", producto.Ubicacion);
                                    sqlCommand.Parameters.AddWithValue("@StockGuardado", medida.Stock);

                                    await sqlCommand.ExecuteNonQueryAsync();
                                }
                            }
                        }
                    }
                } else {
                    List<ProductoDetallesDto> productosDetalles = new List<ProductoDetallesDto>();

                    string sqlQuery = @"
                    SELECT 
                        T3.IDProducto,
                        SUM(T3.Cantidad) AS TotalCantidad,
                        T3.Descripcion,
                        T3.MedidaBase,
                        T3.Fabricante,
                        T3.CodigoFabricante,
                        T3.AbsEntry,
                        T3.Pesado,
                        T3.SL1Code,
                        T3.SL2Code,
                        T3.SL3Code,
                        T3.SL4Code,
                        (SELECT SUM(P1.Peso*P1.Cantidad) FROM PlacaPedido P1 WHERE P1.IDPlanPla = T1.IDPlanPla AND P1.IDProducto = T3.IDProducto) AS PesoTotal,
                        T3.Ubicacion,
                        T4.IDPProducto
                    FROM Planificacion T0
                    INNER JOIN PlanificacionPlaca T1 ON T0.IDPlan = T1.IDPlan
                    INNER JOIN PlacaManifiesto T2 ON T1.IDPlanPla = T2.IDPlanPla
                    INNER JOIN PlacaPedido T3 ON T2.IDPlanMan = T3.IDPlanMan
                    LEFT JOIN PickeoProducto T4 ON T4.IDProducto = T3.IDProducto AND T4.IDPlaca = T1.IDPlanPla
                    LEFT JOIN PickeoProductoIngresado T5 ON T5.IDPProducto = T4.IDPProducto
                    WHERE 
                        T1.IDPlanPla = @plan
                        AND T4.IDPick = @id
                        AND T5.IDPProducto IS NULL
                    GROUP BY 
                        T1.IDPlanPla,
                        T3.IDProducto,
                        T3.Descripcion,
                        T3.MedidaBase,
                        T3.CodigoFabricante,
                        T3.AbsEntry,
                        T3.Fabricante,
                        T3.Pesado,
                        T3.SL1Code,
                        T3.SL2Code,
                        T3.SL3Code,
                        T3.SL4Code,
                        T3.Ubicacion,
                        T4.IDPProducto
                    ORDER BY Pesado, SL1Code, SL2Code, SL3Code, SL4Code";

                    using (var connection = new SqlConnection(_connectionString)) {
                        await connection.OpenAsync();

                        using (var command = new SqlCommand(sqlQuery, connection)) {
                            command.Parameters.AddWithValue("@id", id);
                            command.Parameters.AddWithValue("@plan", plan);

                            using (var reader = await command.ExecuteReaderAsync()) {
                                while (await reader.ReadAsync()) {
                                    var producto = new ProductoDetallesDto {
                                        IDProducto = reader.GetString(reader.GetOrdinal("IDProducto")),
                                        TotalCantidad = reader.GetDecimal(reader.GetOrdinal("TotalCantidad")),
                                        Descripcion = reader.GetString(reader.GetOrdinal("Descripcion")),
                                        MedidaBase = reader.GetString(reader.GetOrdinal("MedidaBase")),
                                        Fabricante = reader.GetString(reader.GetOrdinal("Fabricante")),
                                        CodigoFabricante = reader.GetString(reader.GetOrdinal("CodigoFabricante")),
                                        AbsEntry = reader.GetString(reader.GetOrdinal("AbsEntry")),
                                        Pesado = reader.GetBoolean(reader.GetOrdinal("Pesado")) ? 1 : 0,
                                        SL1Code = reader.GetString(reader.GetOrdinal("SL1Code")),
                                        SL2Code = reader.GetString(reader.GetOrdinal("SL2Code")),
                                        SL3Code = reader.GetString(reader.GetOrdinal("SL3Code")),
                                        SL4Code = reader.GetString(reader.GetOrdinal("SL4Code")),
                                        PesoTotal = reader.GetDecimal(reader.GetOrdinal("PesoTotal")),
                                        Ubicacion = reader.GetString(reader.GetOrdinal("Ubicacion")),
                                        IDPProducto = reader.GetInt32(reader.GetOrdinal("IDPProducto"))
                                    };

                                    using (var hanaConnection = new HanaConnection(_hanaConnectionString)) {
                                        await hanaConnection.OpenAsync();

                                        string sapQuery = $@"
                                    SELECT DISTINCT T1.""UomName"" AS Medida, T1.""UomCode"" AS IdMedida, 1.0 AS Factor, T2.""OnHand"" AS Stock
                                    FROM ITM1 T0
                                    INNER JOIN OUOM T1 ON T1.""UomEntry"" = T0.""UomEntry""
                                    INNER JOIN OITW T2 ON T2.""ItemCode"" = T0.""ItemCode"" AND T2.""WhsCode"" = 'CENTRAL'
                                    WHERE T0.""ItemCode"" = '{producto.IDProducto}'
                                    UNION ALL
                                    SELECT DISTINCT T1.""UomName"" AS Medida, T1.""UomCode"" AS IdMedida, T2.""BaseQty"" AS Factor, T3.""OnHand"" AS Stock
                                    FROM ITM9 T0
                                    INNER JOIN OUOM T1 ON T1.""UomEntry"" = T0.""UomEntry""
                                    INNER JOIN UGP1 T2 ON T2.""UomEntry"" = T1.""UomEntry""
                                    INNER JOIN OITW T3 ON T3.""ItemCode"" = T0.""ItemCode"" AND T3.""WhsCode"" = 'CENTRAL'
                                    WHERE T0.""ItemCode"" = '{producto.IDProducto}'
                                    ORDER BY Factor";

                                        using (var hanaCommand = new HanaCommand(sapQuery, hanaConnection))
                                        using (var hanaReader = await hanaCommand.ExecuteReaderAsync()) {
                                            while (await hanaReader.ReadAsync()) {
                                                var medida = new MedidaProductoDto {
                                                    Medida = hanaReader.GetString(hanaReader.GetOrdinal("Medida")),
                                                    IdMedida = hanaReader.GetString(hanaReader.GetOrdinal("IdMedida")),
                                                    Factor = hanaReader.GetDecimal(hanaReader.GetOrdinal("Factor")),
                                                    Stock = hanaReader.GetDecimal(hanaReader.GetOrdinal("Stock"))
                                                };


                                                using (var sqlConnection = new SqlConnection(_connectionString)) {
                                                    await sqlConnection.OpenAsync();

                                                    string sqlsubQuery = @"
                                                    SELECT SUM(Cantidad) AS CantidadPedida 
                                                    FROM PlacaPedido 
                                                    WHERE IDProducto = @idProducto 
                                                    AND IDPlanPla = @Plan 
                                                    AND Factor = @factor";

                                                    using (var sqlCommand = new SqlCommand(sqlsubQuery, sqlConnection)) {
                                                        sqlCommand.Parameters.AddWithValue("@idProducto", producto.IDProducto);
                                                        sqlCommand.Parameters.AddWithValue("@Plan", plan);
                                                        sqlCommand.Parameters.AddWithValue("@factor", medida.Factor);

                                                        var cantidadPedida = await sqlCommand.ExecuteScalarAsync();
                                                        medida.CantidadPedida = cantidadPedida != DBNull.Value ? Convert.ToDecimal(cantidadPedida) / medida.Factor : 0;
                                                    }
                                                    producto.Medidas.Add(medida);
                                                }
                                            }
                                        }
                                    }

                                    productosDetalles.Add(producto);
                                }
                            }
                        }
                    }

                    using (var sqlConnection = new SqlConnection(_connectionString)) {
                        await sqlConnection.OpenAsync();

                        foreach (var producto in productosDetalles) {
                            foreach (var medida in producto.Medidas) {
                                string insertQuery = @"
                                INSERT INTO PickeoProductoIngresado (IDPProducto, Factor, Medidad, Cantidad, FechaCre, AbsEntry, UbiAbs, StockGuardado)
                                VALUES (@IDPProducto, @Factor, @Medidad, @Cantidad, GETDATE(), @AbsEntry, @UbiAbs, @StockGuardado)";

                                using (var sqlCommand = new SqlCommand(insertQuery, sqlConnection)) {
                                    sqlCommand.Parameters.AddWithValue("@IDPProducto", producto.IDPProducto);
                                    sqlCommand.Parameters.AddWithValue("@Factor", medida.Factor);
                                    sqlCommand.Parameters.AddWithValue("@Medidad", medida.Medida);
                                    sqlCommand.Parameters.AddWithValue("@Cantidad", medida.CantidadPedida);
                                    sqlCommand.Parameters.AddWithValue("@AbsEntry", producto.AbsEntry);
                                    sqlCommand.Parameters.AddWithValue("@UbiAbs", producto.Ubicacion);
                                    sqlCommand.Parameters.AddWithValue("@StockGuardado", medida.Stock);

                                    await sqlCommand.ExecuteNonQueryAsync();
                                }
                            }
                        }
                    }

                }
                return Ok(new { message = "Proceso completado exitosamente." });

                //return Ok(productosDetalles);
            } catch (Exception ex) {
                return StatusCode(500, new { message = "Ocurrió un error al obtener los detalles de picking.", error = ex.Message });
            }
        }





        [HttpGet]
        public async Task<IActionResult> ObtenerPaqueteriasProducto(string idProducto, int idp, int Plan, int abs) {
            HanaConnection hanaConnection = new(_hanaConnectionString);
            try {
                await hanaConnection.OpenAsync();
                string almacenResult = null;
                var result2 = "";

                using (var connection1 = new SqlConnection(_connectionString)) {
                    string query = @"
                        SELECT T1.Tipo 
                        FROM PlanificacionTraslado T0 
                        INNER JOIN CodigosBeetrack T1 ON T0.IDPlanTraslado = T1.IDPlan 
                        INNER JOIN PlanificacionPlacaTransferencia T2 ON T2.IDPlan = T1.IDPlan 
                        WHERE T2.IDPlanPla = @Plan"
                    ;

                    var parameters = new { Plan = Plan };

                    result2 = await connection1.QueryFirstOrDefaultAsync<string>(query, parameters);

                }
                if (result2 == "Transferencia") {

                    using (var connection = new SqlConnection(_connectionString)) {
                        string almacenQuery = @"
                        SELECT TOP 1 T0.AlmacenTraslado 
FROM PlanificacionTraslado T0 
INNER JOIN PlanificacionPlacaTransferencia T1 ON T1.IDPlan = T0.IDPlanTraslado 
                        WHERE IDPlanPla = @Plan";

                        var almacenParam = new { Plan = Plan };
                        almacenResult = await connection.QuerySingleOrDefaultAsync<string>(almacenQuery, almacenParam);

                    }

                    string sapQuery = $@"
                    SELECT DISTINCT T1.""UomName"" AS Medida, T1.""UomCode"" AS IdMedida, 1 AS Factor, T2.""OnHand"" AS Stock
                    FROM ITM1 T0
                    INNER JOIN OUOM T1 ON T1.""UomEntry"" = T0.""UomEntry""
                    INNER JOIN OITW T2 ON T2.""ItemCode"" = T0.""ItemCode"" AND T2.""WhsCode"" = '{almacenResult}'
                    --INNER JOIN UGP1 T4 ON T4.""UomEntry"" = T1.""UomEntry""
                    WHERE T0.""ItemCode"" = '{idProducto}'
                    UNION ALL
                    SELECT DISTINCT T1.""UomName"" AS Medida, T1.""UomCode"" AS IdMedida, T2.""BaseQty"" AS Factor, T3.""OnHand"" AS Stock
                    FROM ITM9 T0
                    INNER JOIN OUOM T1 ON T1.""UomEntry"" = T0.""UomEntry""
                    INNER JOIN UGP1 T2 ON T2.""UomEntry"" = T1.""UomEntry""
                    INNER JOIN OITW T3 ON T3.""ItemCode"" = T0.""ItemCode"" AND T3.""WhsCode"" = '{almacenResult}'
                    WHERE  T0.""ItemCode""  not in ('227002','227001') and  T0.""ItemCode"" = '{idProducto}'
                    ORDER BY Factor";

                    var paqueterias = await hanaConnection.QueryAsync<PaqueteriasProducto>(sapQuery);

                    bool hayRegistros = false;
                    List<int> absEntries = new List<int>();

                    using (var connection = new SqlConnection(_connectionString)) {
                        await connection.OpenAsync();

                        string queryExisten = "SELECT COUNT(DISTINCT AbsEntry) AS Existen FROM PickeoProductoIngresado WHERE IDPProducto = @idp";
                        var existenRegistros = await connection.QueryFirstOrDefaultAsync<int>(queryExisten, new { idp });

                        hayRegistros = existenRegistros > 0;

                        if (hayRegistros) {
                            string queryAbsEntries = "SELECT DISTINCT AbsEntry FROM PickeoProductoIngresado WHERE IDPProducto = @idp";
                            absEntries = (await connection.QueryAsync<int>(queryAbsEntries, new { idp })).ToList();
                        }

                        var registrosCombinados = new List<PaqueteriasProducto>();

                        foreach (var paq in paqueterias) {
                            if (hayRegistros) {
                                foreach (var absEntry in absEntries) {
                                    var copiaPaq = new PaqueteriasProducto {
                                        Medida = paq.Medida,
                                        IdMedida = paq.IdMedida,
                                        Factor = paq.Factor,
                                        Stock = paq.Stock
                                    };

                                    string sqlQuery = "SELECT Cantidad, StockGuardado, UbiAbs FROM PickeoProductoIngresado WHERE IDPProducto = @idp AND Factor = @factor AND AbsEntry = @AbsEntry";
                                    var cantidadRegistro = await connection.QueryFirstOrDefaultAsync<dynamic>(sqlQuery, new { idp, factor = paq.Factor, AbsEntry = absEntry });
                                    copiaPaq.Cantidad = cantidadRegistro?.Cantidad ?? 0;
                                    copiaPaq.StockGuardado = cantidadRegistro?.StockGuardado ?? 0;
                                    copiaPaq.UbiAbs = cantidadRegistro?.UbiAbs ?? string.Empty;
                                    copiaPaq.AbsEntry = absEntry;

                                    string sqlmin = "SELECT SUM(Cantidad) AS CantidadBase FROM PlacaPedidoTransferencia WHERE IDProducto = @idProducto AND IDPlanPla = @Plan AND Factor = @factor";
                                    var cantidadmin = await connection.QueryFirstOrDefaultAsync<int?>(sqlmin, new { idProducto, Plan, factor = paq.Factor });
                                    copiaPaq.Base = cantidadmin ?? 0;

                                    string sqlmax = "SELECT MIN(Factor) AS CantidadBase FROM PlacaPedidoTransferencia WHERE IDProducto = @idProducto AND IDPlanPla = @Plan";
                                    var cantidadmax = await connection.QueryFirstOrDefaultAsync<int?>(sqlmax, new { idProducto, Plan, factor = paq.Factor });
                                    copiaPaq.CantidadBase = cantidadmax ?? 0;

                                    registrosCombinados.Add(copiaPaq);
                                }
                            } else {
                                var copiaPaq = new PaqueteriasProducto {
                                    Medida = paq.Medida,
                                    IdMedida = paq.IdMedida,
                                    Factor = paq.Factor,
                                    Stock = paq.Stock
                                };

                                string sqlQuery = "SELECT Cantidad, StockGuardado, UbiAbs  FROM PickeoProductoIngresado WHERE IDPProducto = @idp AND Factor = @factor";
                                var cantidadRegistro = await connection.QueryFirstOrDefaultAsync<dynamic>(sqlQuery, new { idp, factor = paq.Factor });
                                copiaPaq.Cantidad = cantidadRegistro?.Cantidad ?? 0;
                                copiaPaq.StockGuardado = cantidadRegistro?.StockGuardado ?? 0;
                                copiaPaq.UbiAbs = cantidadRegistro?.UbiAbs ?? string.Empty;
                                copiaPaq.AbsEntry = 0;

                                string sqlmax = "SELECT MIN(Factor) AS CantidadBase FROM PlacaPedidoTransferencia WHERE IDProducto = @idProducto AND IDPlanPla = @Plan";
                                var cantidadmax = await connection.QueryFirstOrDefaultAsync<int?>(sqlmax, new { idProducto, Plan, factor = paq.Factor });
                                copiaPaq.CantidadBase = cantidadmax ?? 0;

                                string sqlmin = "SELECT SUM(Cantidad) AS CantidadBase FROM PlacaPedidoTransferencia WHERE IDProducto = @idProducto AND IDPlanPla = @Plan AND Factor = @factor";
                                var cantidadmin = await connection.QueryFirstOrDefaultAsync<int?>(sqlmin, new { idProducto, Plan, factor = paq.Factor });
                                copiaPaq.Base = cantidadmin ?? 0;

                                registrosCombinados.Add(copiaPaq);
                            }
                        }

                        paqueterias = registrosCombinados;
                    }

                    string stockQuery = $@"
                    SELECT ""OnHandQty""
                    FROM OIBQ 
                    WHERE ""BinAbs"" = '{abs}' AND ""ItemCode"" = '{idProducto}'";
                    decimal stockActual = 0;
                    using (var connection = new HanaConnection(_hanaConnectionString)) {
                        stockActual = await connection.QueryFirstOrDefaultAsync<decimal>(stockQuery);
                    }
                    return Ok(new { paqueterias, hayRegistros, stockActual });


                } else {
                    using (var connection = new SqlConnection(_connectionString)) {
                        string almacenQuery = @"
                        SELECT TOP 1 T0.Almacen 
                        FROM Planificacion T0 
                        INNER JOIN PlanificacionPlaca T1 ON T1.IDPlan = T0.IDPlan 
                        WHERE IDPlanPla = @Plan";

                        var almacenParam = new { Plan = Plan };
                        almacenResult = await connection.QuerySingleOrDefaultAsync<string>(almacenQuery, almacenParam);

                    }

                    string sapQuery = $@"
                    SELECT DISTINCT T1.""UomName"" AS Medida, T1.""UomCode"" AS IdMedida, 1 AS Factor, T2.""OnHand"" AS Stock
                    FROM ITM1 T0
                    INNER JOIN OUOM T1 ON T1.""UomEntry"" = T0.""UomEntry""
                    INNER JOIN OITW T2 ON T2.""ItemCode"" = T0.""ItemCode"" AND T2.""WhsCode"" = '{almacenResult}'
                    --INNER JOIN UGP1 T4 ON T4.""UomEntry"" = T1.""UomEntry""
                    WHERE T0.""ItemCode"" = '{idProducto}'
                    UNION ALL
                    SELECT DISTINCT T1.""UomName"" AS Medida, T1.""UomCode"" AS IdMedida, T2.""BaseQty"" AS Factor, T3.""OnHand"" AS Stock
                    FROM ITM9 T0
                    INNER JOIN OUOM T1 ON T1.""UomEntry"" = T0.""UomEntry""
                    INNER JOIN UGP1 T2 ON T2.""UomEntry"" = T1.""UomEntry""
                    INNER JOIN OITW T3 ON T3.""ItemCode"" = T0.""ItemCode"" AND T3.""WhsCode"" = '{almacenResult}'
                    WHERE  T0.""ItemCode""  not in ('227002','227001') and  T0.""ItemCode"" = '{idProducto}'
                    ORDER BY Factor";

                    var paqueterias = await hanaConnection.QueryAsync<PaqueteriasProducto>(sapQuery);

                    bool hayRegistros = false;
                    List<int> absEntries = new List<int>();

                    using (var connection = new SqlConnection(_connectionString)) {
                        await connection.OpenAsync();

                        string queryExisten = "SELECT COUNT(DISTINCT AbsEntry) AS Existen FROM PickeoProductoIngresado WHERE IDPProducto = @idp";
                        var existenRegistros = await connection.QueryFirstOrDefaultAsync<int>(queryExisten, new { idp });

                        hayRegistros = existenRegistros > 0;

                        if (hayRegistros) {
                            string queryAbsEntries = "SELECT DISTINCT AbsEntry FROM PickeoProductoIngresado WHERE IDPProducto = @idp";
                            absEntries = (await connection.QueryAsync<int>(queryAbsEntries, new { idp })).ToList();
                        }

                        var registrosCombinados = new List<PaqueteriasProducto>();

                        foreach (var paq in paqueterias) {
                            if (hayRegistros) {
                                foreach (var absEntry in absEntries) {
                                    var copiaPaq = new PaqueteriasProducto {
                                        Medida = paq.Medida,
                                        IdMedida = paq.IdMedida,
                                        Factor = paq.Factor,
                                        Stock = paq.Stock
                                    };

                                    string sqlQuery = "SELECT Cantidad, StockGuardado, UbiAbs FROM PickeoProductoIngresado WHERE IDPProducto = @idp AND Factor = @factor AND AbsEntry = @AbsEntry";
                                    var cantidadRegistro = await connection.QueryFirstOrDefaultAsync<dynamic>(sqlQuery, new { idp, factor = paq.Factor, AbsEntry = absEntry });
                                    copiaPaq.Cantidad = cantidadRegistro?.Cantidad ?? 0;
                                    copiaPaq.StockGuardado = cantidadRegistro?.StockGuardado ?? 0;
                                    copiaPaq.UbiAbs = cantidadRegistro?.UbiAbs ?? string.Empty;
                                    copiaPaq.AbsEntry = absEntry;

                                    string sqlmin = "SELECT SUM(Cantidad) AS CantidadBase FROM PlacaPedido WHERE IDProducto = @idProducto AND IDPlanPla = @Plan AND Factor = @factor";
                                    var cantidadmin = await connection.QueryFirstOrDefaultAsync<int?>(sqlmin, new { idProducto, Plan, factor = paq.Factor });
                                    copiaPaq.Base = cantidadmin ?? 0;

                                    string sqlmax = "SELECT MIN(Factor) AS CantidadBase FROM PlacaPedido WHERE IDProducto = @idProducto AND IDPlanPla = @Plan";
                                    var cantidadmax = await connection.QueryFirstOrDefaultAsync<int?>(sqlmax, new { idProducto, Plan, factor = paq.Factor });
                                    copiaPaq.CantidadBase = cantidadmax ?? 0;

                                    registrosCombinados.Add(copiaPaq);
                                }
                            } else {
                                var copiaPaq = new PaqueteriasProducto {
                                    Medida = paq.Medida,
                                    IdMedida = paq.IdMedida,
                                    Factor = paq.Factor,
                                    Stock = paq.Stock
                                };

                                string sqlQuery = "SELECT Cantidad, StockGuardado, UbiAbs  FROM PickeoProductoIngresado WHERE IDPProducto = @idp AND Factor = @factor";
                                var cantidadRegistro = await connection.QueryFirstOrDefaultAsync<dynamic>(sqlQuery, new { idp, factor = paq.Factor });
                                copiaPaq.Cantidad = cantidadRegistro?.Cantidad ?? 0;
                                copiaPaq.StockGuardado = cantidadRegistro?.StockGuardado ?? 0;
                                copiaPaq.UbiAbs = cantidadRegistro?.UbiAbs ?? string.Empty;
                                copiaPaq.AbsEntry = 0;

                                string sqlmax = "SELECT MIN(Factor) AS CantidadBase FROM PlacaPedido WHERE IDProducto = @idProducto AND IDPlanPla = @Plan";
                                var cantidadmax = await connection.QueryFirstOrDefaultAsync<int?>(sqlmax, new { idProducto, Plan, factor = paq.Factor });
                                copiaPaq.CantidadBase = cantidadmax ?? 0;

                                string sqlmin = "SELECT SUM(Cantidad) AS CantidadBase FROM PlacaPedido WHERE IDProducto = @idProducto AND IDPlanPla = @Plan AND Factor = @factor";
                                var cantidadmin = await connection.QueryFirstOrDefaultAsync<int?>(sqlmin, new { idProducto, Plan, factor = paq.Factor });
                                copiaPaq.Base = cantidadmin ?? 0;

                                registrosCombinados.Add(copiaPaq);
                            }
                        }

                        paqueterias = registrosCombinados;
                    }

                    string stockQuery = $@"
                    SELECT ""OnHandQty""
                    FROM OIBQ 
                    WHERE ""BinAbs"" = '{abs}' AND ""ItemCode"" = '{idProducto}'";
                    decimal stockActual = 0;
                    using (var connection = new HanaConnection(_hanaConnectionString)) {
                        stockActual = await connection.QueryFirstOrDefaultAsync<decimal>(stockQuery);
                    }
                    return Ok(new { paqueterias, hayRegistros, stockActual });

                }






            } catch (Exception ex) {
                return BadRequest(ex.Message);
            } finally {
                if (hanaConnection.State == System.Data.ConnectionState.Open) {
                    hanaConnection.Close();
                }
            }
        }

        [HttpGet]
        public async Task<IActionResult> ConsultarUbicacionesProducto(string codigo, string abs, int idplan) {
            using (var connection = new SqlConnection(_connectionString)) {

                HanaConnection hanaConnection = new(_hanaConnectionString);
                try {
                    await connection.OpenAsync();

                    var almacenQuery = "SELECT TOP 1 T0.Almacen FROM Planificacion T0 INNER JOIN PlanificacionPlaca T1 ON T1.IDPlan = T0.IDPlan WHERE IDPlanPla = @idplan";

                    var almacen = await connection.QueryFirstOrDefaultAsync<string>(almacenQuery, new { idplan });

                    if (string.IsNullOrEmpty(almacen)) {
                        return BadRequest("No se encontró el almacén para el ID de plan especificado.");
                    }

                    await hanaConnection.OpenAsync();

                    string sapQuery = $@"
                        SELECT 
                            T1.""BinCode"",
                            T1.""SL1Code"",
                            T1.""SL2Code"",
                            T1.""SL3Code"",
                            T1.""SL4Code"",
                            T1.""AbsEntry"",
                            T2.""InvntryUom"",
                            T0.""OnHandQty""
                        FROM OIBQ T0
                        INNER JOIN OBIN T1 ON T0.""BinAbs"" = T1.""AbsEntry""
                        INNER JOIN OITM T2 ON T2.""ItemCode"" = T0.""ItemCode""
                        INNER JOIN OMRC T3 ON T3.""FirmCode"" = T2.""FirmCode""
                        WHERE T0.""ItemCode"" = '{codigo}' AND T0.""WhsCode"" = '{almacen}' AND T0.""OnHandQty"" > 0 AND T1.""AbsEntry"" NOT IN ({abs})

                        UNION

                        SELECT 
                            T0.""BinCode"",
                            T0.""SL1Code"",
                            T0.""SL2Code"",
                            T0.""SL3Code"",
                            T0.""SL4Code"",
                            T0.""AbsEntry"",
                            T2.""InvntryUom"",
                            T1.""OnHandQty""
                        FROM OBIN T0
                        LEFT JOIN OIBQ T1 ON T1.""BinAbs"" = T0.""AbsEntry"" AND T1.""ItemCode"" = '{codigo}'
                        LEFT JOIN OITM T2 ON T2.""ItemCode"" = T1.""ItemCode""
                        LEFT JOIN OMRC T3 ON T3.""FirmCode"" = T2.""FirmCode"" 
                        WHERE T0.""AbsEntry"" = (SELECT ""DftBinAbs"" FROM OITW WHERE ""WhsCode"" = '{almacen}' AND ""ItemCode"" = '{codigo}' AND ""DftBinAbs"" NOT IN ({abs}))";
                    var paqueterias = await hanaConnection.QueryAsync<UbicacionesProducto>(sapQuery);

                    return Ok(paqueterias);
                } catch (Exception ex) {
                    return BadRequest(ex.Message);
                } finally {
                    if (hanaConnection.State == System.Data.ConnectionState.Open) {
                        hanaConnection.Close();
                    }
                }
            }
        }


        [HttpPost]
        public async Task<IActionResult> ActualizarPaqueterias([FromBody] UpdateRequest request, string idm, string idpp) {
            using (var connection = new SqlConnection(_connectionString)) {
                await connection.OpenAsync();

                string updatePickeoProductoQuery = "UPDATE PickeoProducto SET Motivo = @idm WHERE IDPProducto = @idpp";
                var updatePickeoProductoParams = new {
                    idm = idm,
                    idpp = idpp
                };
                await connection.ExecuteAsync(updatePickeoProductoQuery, updatePickeoProductoParams);

                foreach (var registro in request.Registros) {
                    string query = "UPDATE PickeoProductoIngresado SET Cantidad = @Cantidad, Fecha = GETDATE() WHERE IDPProducto = @IdProducto AND Factor = @Factor AND AbsEntry = @Abs";
                    var parameters = new {
                        Cantidad = registro.Cantidad,
                        IdMedida = registro.IdMedida,
                        Factor = registro.Factor,
                        IdProducto = request.IdpProducto,
                        Abs = registro.abs
                    };
                    await connection.ExecuteAsync(query, parameters);
                }
            }
            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> VerificarCheck(int idp, int idPlan) {
            using (var connection = new SqlConnection(_connectionString)) {
                await connection.OpenAsync();

                string query = "UPDATE PickeoProducto SET Verificado = 1 WHERE IDProducto = @idp AND IDPlaca = @idPlan";

                using (var command = new SqlCommand(query, connection)) {
                    command.Parameters.AddWithValue("@idp", idp);
                    command.Parameters.AddWithValue("@idPlan", idPlan);

                    int rowsAffected = await command.ExecuteNonQueryAsync();

                    if (rowsAffected > 0) {
                        return Ok(new { message = "Producto verificado con éxito." });
                    } else {
                        return NotFound(new { message = "No se encontró el producto con los parámetros proporcionados." });
                    }
                }
            }
        }

        [HttpPost]
        public async Task<IActionResult> MarcarPendienteProducto(int idp, int idPlan, int idpick) {
            using (var connection = new SqlConnection(_connectionString)) {
                await connection.OpenAsync();

                string queryupdate = @"
                    UPDATE PickeoProducto
                    SET FechaVeriIni = GETDATE()
                    WHERE IDPlaca = @idPlan AND IDPick = @idpick AND FechaVeriIni IS NULL";

                var parameterss = new { idPlan, idpick };

                var results = await connection.ExecuteAsync(queryupdate, parameterss);

                string query = "UPDATE PickeoProducto SET Marcado = 1 WHERE IDProducto = @idp AND IDPlaca = @idPlan";

                using (var command = new SqlCommand(query, connection)) {
                    command.Parameters.AddWithValue("@idp", idp);
                    command.Parameters.AddWithValue("@idPlan", idPlan);

                    int rowsAffected = await command.ExecuteNonQueryAsync();

                    if (rowsAffected > 0) {
                        return Ok(new { message = "Producto marcado con éxito." });
                    } else {
                        return NotFound(new { message = "No se encontró el producto con los parámetros proporcionados." });
                    }
                }
            }
        }

        [HttpPost]
        public async Task<IActionResult> ActualizarPaqueteriasVeri([FromBody] UpdateRequest request) {
            using (var connection = new SqlConnection(_connectionString)) {
                await connection.OpenAsync();
                foreach (var registro in request.Registros) {
                    string query = "UPDATE PickeoProductoIngresado SET Cantidad = @Cantidad WHERE IDPProducto = @IdProducto AND Factor = @Factor AND AbsEntry = @AbsEntry";
                    var parameters = new {
                        Cantidad = registro.Cantidad,
                        IdMedida = registro.IdMedida,
                        Factor = registro.Factor,
                        IdProducto = request.IdpProducto,
                        AbsEntry = registro.abs
                    };
                    await connection.ExecuteAsync(query, parameters);
                    //string querypp = "UPDATE PickeoProducto SET Verificado = 1 WHERE IDProducto = @IdProducto AND IDPlaca = @IDPlan";
                    //var parameterspp = new {
                    //    IDPlan= request.IdPlan,
                    //    IdProducto = request.IdProducto
                    //};
                    //await connection.ExecuteAsync(querypp, parameterspp);
                }
            }
            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> EnviarCargadaJefe(int idPlanPla) {

            var result2 = "";

            using (var connection1 = new SqlConnection(_connectionString)) {
                string query = @"
                        SELECT T1.Tipo 
                        FROM PlanificacionTraslado T0 
                        INNER JOIN CodigosBeetrack T1 ON T0.IDPlanTraslado = T1.IDPlan 
                        INNER JOIN PlanificacionPlacaTransferencia T2 ON T2.IDPlan = T1.IDPlan 
                        WHERE T2.IDPlanPla = @idPlanPla"
                ;

                var parameters = new { idPlanPla = idPlanPla };

                result2 = await connection1.QueryFirstOrDefaultAsync<string>(query, parameters);

            }
            if (result2 == "Transferencia") {

                try {
                    using (var connection = new SqlConnection(_connectionString)) {
                        await connection.OpenAsync();

                        string query1 = @"
                        UPDATE PlacaPedidoTransferencia 
                        SET CantidadCargar = Cantidad, RevisadoCoor = 1
                        WHERE IDPlanPla = @IdPlanPla";

                        var parameters1 = new { IdPlanPla = idPlanPla };
                        await connection.ExecuteAsync(query1, parameters1);

                        string query2 = @"
                        UPDATE PlanificacionPlacaTransferencia 
                        SET Enviado = 1, Cargar = 1
                        WHERE IDPlanPla = @IdPlanPla";

                        var parameters2 = new { IdPlanPla = idPlanPla };
                        await connection.ExecuteAsync(query2, parameters2);
                    }

                    return Ok(new { message = "Carga enviada con éxito" });
                } catch (Exception ex) {
                    return StatusCode(500, new { message = "Error al procesar la solicitud", error = ex.Message });
                }

            } else {
                try {
                    using (var connection = new SqlConnection(_connectionString)) {
                        await connection.OpenAsync();

                        string query1 = @"
                        UPDATE PlacaPedido 
                        SET CantidadCargar = Cantidad, RevisadoCoor = 1
                        WHERE IDPlanPla = @IdPlanPla";

                        var parameters1 = new { IdPlanPla = idPlanPla };
                        await connection.ExecuteAsync(query1, parameters1);

                        string query2 = @"
                        UPDATE PlanificacionPlaca 
                        SET Enviado = 1, Cargar = 1
                        WHERE IDPlanPla = @IdPlanPla";

                        var parameters2 = new { IdPlanPla = idPlanPla };
                        await connection.ExecuteAsync(query2, parameters2);
                    }

                    return Ok(new { message = "Carga enviada con éxito" });
                } catch (Exception ex) {
                    return StatusCode(500, new { message = "Error al procesar la solicitud", error = ex.Message });
                }
            }







        }


        [HttpPost]
        public async Task<IActionResult> GuardarNuevaUbicacion(string id, string bc, string ae, string l1, string l2, string l3, string l4, int idPlan, string cod) {
            try {
                using (var connection = new SqlConnection(_connectionString)) {
                    await connection.OpenAsync();

                    string query = "UPDATE PlacaPedido SET Ubicacion = @BinCode, AbsEntry = @AbsEntry, SL1Code = @SL1Code, SL2Code = @SL2Code, SL3Code = @SL3Code, SL4Code = @SL4Code  WHERE IDPlanPla = @IDPlan AND IDProducto = @Cod";
                    var parameters = new {
                        BinCode = bc,
                        AbsEntry = ae,
                        SL1Code = l1,
                        SL2Code = l2,
                        SL3Code = l3,
                        SL4Code = l4,
                        IDPlan = idPlan,
                        Cod = cod
                    };

                    await connection.ExecuteAsync(query, parameters);

                    return Ok(new { success = true, message = "Ubicación actualizada correctamente." });
                }
            } catch (Exception ex) {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> AgregarNuevaUbicacion(string id, string bc, string ae, string l1, string l2, string l3, string l4, int idPlan, string cod) {
            try {
                using (var connection = new SqlConnection(_connectionString)) {
                    await connection.OpenAsync();

                    string query = "UPDATE PlacaPedido SET Ubicacion = Ubicacion";
                    var parameters = new {
                        BinCode = bc,
                        AbsEntry = ae,
                        SL1Code = l1,
                        SL2Code = l2,
                        SL3Code = l3,
                        SL4Code = l4,
                        IDPlan = idPlan,
                        Cod = cod
                    };

                    await connection.ExecuteAsync(query, parameters);

                    return Ok(new { success = true, message = "Ubicación actualizada correctamente." });
                }
            } catch (Exception ex) {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }


        [HttpPost]
        public async Task<IActionResult> ReconteoProducto(int idPlan, string idProducto) {
            using (var connection = new SqlConnection(_connectionString)) {
                await connection.OpenAsync();
                string query = $@"
                    UPDATE PickeoProducto SET Finalizado = 0, Reconteo = 1 WHERE IDPlaca = @idPlan AND IDProducto IN ({idProducto})";
                var parameters = new { idPlan };

                var result = await connection.ExecuteAsync(query, parameters);

                if (result > 0) {
                    return Ok(new { success = true, message = "Enviado a reconteo exitosamente." });
                } else {
                    return NotFound(new { success = false, message = "No se encontraron registros para actualizar." });
                }
            }
        }

        [HttpPost]
        public async Task<IActionResult> AceptarProducto(int idPlan, string idProducto) {
            using (var connection = new SqlConnection(_connectionString)) {
                await connection.OpenAsync();
                string query = $@"
                    UPDATE PickeoProducto SET Finalizado = 1, Reconteo = 1, Aceptado = 1 WHERE IDPlaca = @idPlan AND IDProducto IN ({idProducto})";
                var parameters = new { idPlan };

                var result = await connection.ExecuteAsync(query, parameters);

                if (result > 0) {
                    return Ok(new { success = true, message = "Enviado a reconteo exitosamente." });
                } else {
                    return NotFound(new { success = false, message = "No se encontraron registros para actualizar." });
                }
            }
        }

        [HttpGet]
        public async Task<IActionResult> CargarPersonalPickeo(string id, string almacenubicacion) {
            using (var connection = new SqlConnection(_connectionString)) {
                string queryUsuario = @"
                    SELECT Usuario FROM PlanificacionPlaca WHERE IDPlanPla = @id";
                var usuario = await connection.QueryFirstOrDefaultAsync<string>(queryUsuario, new { id });

                if (usuario == null) {
                    return NotFound("Usuario no encontrado.");
                }
                string queryPickeoPersonal = @"SELECT IDPP, Nombre FROM PickeoPersonal WHERE Almacen = @almacenubicacion";

                var result = await connection.QueryAsync(queryPickeoPersonal, new { usuario, almacenubicacion });
                return Ok(result);
            }
        }

        [HttpGet]
        public async Task<IActionResult> CargarMotivos(string id) {
            using (var connection = new SqlConnection(_connectionString)) {

                string queryPickeoPersonal = @"
                    SELECT IDMotivo, DesMotivo 
                    FROM PickeoMotivos";

                var result = await connection.QueryAsync(queryPickeoPersonal);

                return Ok(result);
            }
        }

        [HttpGet]
        public async Task<IActionResult> CargarPersonalJefes(string almacen) {
            using (var connection = new SqlConnection(_connectionString)) {
                string query = @"
            SELECT IDPP, Nombre 
            FROM PickeoPersonal 
            WHERE Almacen = @Almacen";

                var result = await connection.QueryAsync(query, new { Almacen = almacen });

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
                            UPDATE PlanificacionPlaca 
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

        [HttpPost]
        public async Task<IActionResult> ActualizarProductosAsignados([FromBody] AsignacionProductosRequest request) {
            if (request.Productos == null || !request.Productos.Any()) {
                return BadRequest("No se han proporcionado productos para asignar.");
            }

            using (var connection = new SqlConnection(_connectionString)) {
                await connection.OpenAsync();

                using (var transaction = await connection.BeginTransactionAsync()) {
                    try {

                        foreach (var producto in request.Productos) {
                            string insertQuery = @"
                                UPDATE PickeoProducto SET IDPick = @IDPickeador WHERE IDPlaca = @IDPlan AND IDPick =@IDPick AND IDProducto = @IDProducto ";

                            var parameters = new {
                                IDProducto = producto.Id,
                                IDPickeador = request.PickeadorId,
                                IDPlan = request.IdPlan,
                                IDPick = request.IDPick
                            };
                            await connection.ExecuteAsync(insertQuery, parameters, transaction);
                        }

                        await transaction.CommitAsync();
                        return Ok(new { success = true });
                    } catch (Exception ex) {
                        await transaction.RollbackAsync();
                        return StatusCode(500, $"Error al actualizar los productos asignados: {ex.Message}");
                    }
                }
            }
        }

        [HttpPost]
        public async Task<IActionResult> GuardarPaqueterias([FromBody] List<ProductosIngresados> productos, string idm, string idpp) {
            if (productos == null || !productos.Any()) {
                return BadRequest("No se han proporcionado productos para asignar.");
            }

            using (var connection = new SqlConnection(_connectionString)) {
                await connection.OpenAsync();

                using (var transaction = await connection.BeginTransactionAsync()) {
                    try {

                        string updateQuery = @"
                            UPDATE PickeoProducto 
                            SET Motivo = @idm 
                            WHERE IDPProducto = @idpp";

                        var updateParameters = new {
                            idm = idm,
                            idpp = idpp
                        };

                        await connection.ExecuteAsync(updateQuery, updateParameters, transaction);


                        foreach (var producto in productos) {
                            string query = @"
                        INSERT INTO PickeoProductoIngresado (IDPProducto, Factor, Medidad, Cantidad, FechaCre, AbsEntry, UbiAbs, StockGuardado)
                        VALUES (@IDPProducto, @Factor, @Medidad, @Cantidad, GETDATE(), @AbsEntry, @UbiAbs, @StockGuardado)";

                            var parameters = new {
                                IDPProducto = producto.idpProducto,
                                Factor = producto.factor,
                                Medidad = producto.idMedida,
                                Cantidad = producto.cantidad,
                                AbsEntry = producto.absEntry,
                                UbiAbs = producto.ubiAbs,
                                StockGuardado = producto.stockGuardado
                            };

                            await connection.ExecuteAsync(query, parameters, transaction);
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

        [HttpPost]
        public async Task<IActionResult> FinalizarConteoPlaca(int idPlan, int idpp) {

            using (var connection = new SqlConnection(_connectionString)) {
                await connection.OpenAsync();

                using (var transaction = await connection.BeginTransactionAsync()) {
                    try {
                        string updatePickeoProductoQuery = @"
                            UPDATE PickeoProducto 
                            SET Finalizado = 1, Aceptado = 1, FechaFin = GETDATE() 
                            WHERE IDPlaca = @idPlan AND IDPick = @idpp";

                        var pickeoParameters = new { idpp, idPlan };
                        var resultPickeo = await connection.ExecuteAsync(updatePickeoProductoQuery, pickeoParameters, transaction);
                        ObtenerUsuarioYClave();
                        if (resultPickeo > 0) {
                            await transaction.CommitAsync();
                            return Ok(new { success = true, message = "Conteo finalizado exitosamente.", puesto, idpickador });
                        } else {
                            await transaction.RollbackAsync();
                            return NotFound(new { success = false, message = "No se encontraron registros para actualizar." });
                        }
                    } catch (Exception ex) {
                        await transaction.RollbackAsync();
                        return StatusCode(500, $"Error al finalizar el conteo: {ex.Message}");
                    }
                }
            }
        }

        [HttpPost]
        public async Task<IActionResult> VerificarConteoPlaca(int idPlan) {
            using (var connection = new SqlConnection(_connectionString)) {
                await connection.OpenAsync();
                string query = @"
                    UPDATE PickeoProducto SET Verificado = 1 WHERE IDPlaca = @idPlan";
                var parameters = new { idPlan };
                var result = await connection.ExecuteAsync(query, parameters);

                string querys = @"
                    UPDATE PlanificacionPlaca SET Enviado = 1, FechaEnvio = GETDATE()  WHERE IDPlanPla = @idPlan";
                var parameterss = new { idPlan };
                var results = await connection.ExecuteAsync(querys, parameterss);

                if (result > 0) {
                    return Ok(new { success = true, message = "Verificacion finalizado exitosamente." });
                } else {
                    return NotFound(new { success = false, message = "No se encontraron registros para actualizar." });
                }
            }
        }

        [HttpPost]
        public async Task<IActionResult> VeriProducto(string idProducto, int idPlan) {
            using (var connection = new SqlConnection(_connectionString)) {
                await connection.OpenAsync();
                string query = @"
                    UPDATE PickeoProducto SET Verificado = 1 WHERE IDPlaca = @idPlan AND IDProducto = @idProducto";
                var parameters = new { idPlan, idProducto };

                var result = await connection.ExecuteAsync(query, parameters);

                if (result > 0) {
                    return Ok(new { success = true, message = "Verificacion finalizado exitosamente." });
                } else {
                    return NotFound(new { success = false, message = "No se encontraron registros para actualizar." });
                }
            }
        }

        [HttpPost]
        public async Task<IActionResult> VerificarSeleccionados(string ids, int idPlan, int idpick) {
            var idList = ids.Split(',').Select(id => Convert.ToInt32(id)).ToList();

            using (var connection = new SqlConnection(_connectionString)) {
                await connection.OpenAsync();

                string query = @"
                    UPDATE PickeoProducto
                    SET Verificado = 1
                    WHERE IDPlaca = @idPlan AND IDProducto IN @ids";

                var parameters = new { idPlan, ids = idList };

                var result = await connection.ExecuteAsync(query, parameters);

                string queryupdate = @"
                    UPDATE PickeoProducto
                    SET FechaVeriIni = GETDATE()
                    WHERE IDPlaca = @idPlan AND IDPick = @idpick AND FechaVeriIni IS NULL";

                var parameterss = new { idPlan, idpick };

                var results = await connection.ExecuteAsync(queryupdate, parameterss);

                string queryupdates = @"
                    UPDATE PickeoProducto
                    SET FechaVeriFin = GETDATE()
                    WHERE IDPlaca = @idPlan AND IDPick = @idpick AND FechaVeriIni IS NOT NULL";

                var resultss = await connection.ExecuteAsync(queryupdates, parameterss);

                if (result > 0) {
                    return Ok(new { success = true, message = "Verificación finalizada exitosamente." });
                } else {
                    return NotFound(new { success = false, message = "No se encontraron registros para actualizar." });
                }
            }
        }

        [HttpPost]
        public async Task<IActionResult> GuardarJefePlaca(int idPlanPla, int idPickeador) {
            string query = null;
            if (idPickeador == 0) {
                query = @"
                    UPDATE PlanificacionPlaca SET Usuario = NULL, FechaJefe = GETDATE() WHERE IDPlanPla = @idPlanPla";
            } else {
                query = @"
                    UPDATE PlanificacionPlaca SET Usuario = @idPickeador, FechaJefe = GETDATE() WHERE IDPlanPla = @idPlanPla";
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

        [HttpPost]
        public async Task<IActionResult> EnviarACoordinador(int idPlanPla) {
            var result2 = "";

            using (var connection1 = new SqlConnection(_connectionString)) {
                string query = @"
                        SELECT T1.Tipo 
                        FROM PlanificacionTraslado T0 
                        INNER JOIN CodigosBeetrack T1 ON T0.IDPlanTraslado = T1.IDPlan 
                        INNER JOIN PlanificacionPlacaTransferencia T2 ON T2.IDPlan = T1.IDPlan 
                        WHERE T2.IDPlanPla = @idPlanPla"
                ;

                var parameters = new { idPlanPla = idPlanPla };

                result2 = await connection1.QueryFirstOrDefaultAsync<string>(query, parameters);

            }

            using (var connection = new SqlConnection(_connectionString)) {
                await connection.OpenAsync();

                if (result2 == "Transferencia") {

                    string query = @"
                    UPDATE PlanificacionPlacaTransferencia SET Enviado = 1, FechaEnvio = GETDATE()  WHERE IDPlanPla = @idPlanPla";
                    var parameters = new { idPlanPla };

                    var result = await connection.ExecuteAsync(query, parameters);

                    if (result > 0) {
                        return Ok(new { success = true, message = "Conteo finalizado exitosamente." });
                    } else {
                        return NotFound(new { success = false, message = "No se encontraron registros para actualizar." });
                    }

                } else {
                    string query = @"
                    UPDATE PlanificacionPlaca SET Enviado = 1, FechaEnvio = GETDATE()  WHERE IDPlanPla = @idPlanPla";
                    var parameters = new { idPlanPla };

                    var result = await connection.ExecuteAsync(query, parameters);

                    if (result > 0) {
                        return Ok(new { success = true, message = "Conteo finalizado exitosamente." });
                    } else {
                        return NotFound(new { success = false, message = "No se encontraron registros para actualizar." });
                    }
                }



                
            }
        }









    }
}
