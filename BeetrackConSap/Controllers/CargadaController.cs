using Microsoft.AspNetCore.Mvc;
using System.Data.SqlClient;
using Sap.Data.Hana;
using Dapper;
using BeetrackConSap.Models;
using Microsoft.AspNetCore.Identity.Data;
using System.Numerics;



namespace BeetrackConSap.Controllers {
    public class CargadaController : Controller {

        private readonly string _hanaConnectionString;
        private readonly string _connectionString;

        public CargadaController(IConfiguration configuration) {
            _connectionString = configuration.GetConnectionString("Femaco10");
            _hanaConnectionString = configuration.GetConnectionString("FemacoSap");
        }
        public IActionResult Index() {
            return View("~/Views/Cargada/CargadaVista.cshtml");
        }
        public IActionResult CargadaVista() {
            return View("~/Views/Cargada/CargadaVista.cshtml");
        }
        public IActionResult CargadaProductos() {
            return View("~/Views/Cargada/CargadaProductos.cshtml");
        }
        public IActionResult CargadaIncompleta() {
            return View("~/Views/Cargada/CargadaIncompleta.cshtml");
        }
        [HttpGet]
        public async Task<IActionResult> ObtenerPlacaPlan() {
            using (var connection = new SqlConnection(_connectionString)) {
                string query = @"
                    SELECT 
                    T1.Placa,
                    T1.IDPlanPla,
                    COUNT(T2.IDPlaca) AS Items,
                    COUNT(T3.IDPlaca) AS Finalizados,
                    COALESCE(T1.Cargar,0) AS Cargar,
                    COALESCE(T1.Cargado,0) AS Cargado
                    FROM Planificacion T0
                    INNER JOIN PlanificacionPlaca T1 ON T0.IDPlan = T1.IDPlan
                    LEFT JOIN PickeoProducto T2 ON T2.IDPlaca = T1.IDPlanPla
                    LEFT JOIN PickeoProducto T3 ON T3.IDPProducto = T2.IDPProducto AND T3.Finalizado = 1
                    GROUP BY T1.Placa, T1.IDPlanPla, T1.Cargar, T1.Cargado";

                var result = await connection.QueryAsync<CargaPlan>(query);
                return Ok(result);
            }
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerProductos(int id) {
            var result2 = "";

            using (var connection1 = new SqlConnection(_connectionString)) {
                string query = @"
                        SELECT T1.Tipo 
                        FROM PlanificacionTraslado T0 
                        INNER JOIN CodigosBeetrack T1 ON T0.IDPlanTraslado = T1.IDPlan 
                        INNER JOIN PlanificacionPlacaTransferencia T2 ON T2.IDPlan = T1.IDPlan 
                        WHERE T2.IDPlanPla = @id"
                ;

                var parameters = new { id = id };

                result2 = await connection1.QueryFirstOrDefaultAsync<string>(query, parameters);

            }
            if (result2 == "Transferencia") {
                using (var connection = new SqlConnection(_connectionString)) {
                    string query = @"
                    SELECT 
                    T0.IDPProducto,
                    T0.IDProducto, 
                    (SELECT SUM(P1.Cantidad*P1.Factor) FROM PickeoProductoIngresado P1 WHERE P1.IDPProducto = T2.IDPProducto) AS Cantidad,
                    (SELECT SUM(P2.Cargado*P2.Factor) FROM PickeoProductoIngresado P2 WHERE P2.IDPProducto = T2.IDPProducto AND P2.AbsEntry = T1.AbsEntry) AS Cargado,
                    MAX(T1.Descripcion) AS Descripcion,
                    T1.SL1Code,
                    T1.SL2Code,
                    T1.SL3Code,
                    T1.SL4Code,
                    COALESCE(T2.CargaEstado,0) AS Estado,
                    COALESCE(T2.Confirma,0) AS Confirma,
                    T3.CargaIncompleta,
                    T3.Confirmado
                    FROM PickeoProducto T0 
                    LEFT JOIN PlacaPedidoTransferencia T1 ON T1.IDProducto = T0.IDProducto and T1.IDPlanPla = T0.IDPlaca
                    INNER JOIN PickeoProductoIngresado T2 ON T2.IDPProducto = T0.IDPProducto 
                    LEFT JOIN PlanificacionPlacaTransferencia T3 ON T3.IDPlanPla = T0.IDPlaca
                    WHERE T0.IDPlaca = @id
                    AND T2.Cantidad != 0
                    GROUP BY 
                    T0.IDPProducto,
                    T0.IDProducto,
                    T0.Cantidad,
                    T1.SL1Code,
                    T1.SL2Code,
                    T1.SL3Code,
                    T1.SL4Code,
                    T2.IDPProducto,
                    T2.CargaEstado,
                    T2.Confirma,
                    T1.AbsEntry,
                    T3.CargaIncompleta,
                    T3.Confirmado";


                    var parameters = new { id };
                    var result = await connection.QueryAsync<ProductosContador>(query, parameters);
                    return Ok(result);
                }


            } else {
                using (var connection = new SqlConnection(_connectionString)) {
                    string query = @"
                    SELECT 
                    T0.IDPProducto,
                    T0.IDProducto, 
                    (SELECT SUM(P1.Cantidad*P1.Factor) FROM PickeoProductoIngresado P1 WHERE P1.IDPProducto = T2.IDPProducto) AS Cantidad,
                    (SELECT SUM(P2.Cargado*P2.Factor) FROM PickeoProductoIngresado P2 WHERE P2.IDPProducto = T2.IDPProducto AND P2.AbsEntry = T1.AbsEntry) AS Cargado,
                    MAX(T1.Descripcion) AS Descripcion,
                    T1.SL1Code,
                    T1.SL2Code,
                    T1.SL3Code,
                    T1.SL4Code,
                    COALESCE(T2.CargaEstado,0) AS Estado,
                    COALESCE(T2.Confirma,0) AS Confirma,
                    T3.CargaIncompleta,
                    T3.Confirmado
                    FROM PickeoProducto T0 
                    LEFT JOIN PlacaPedido T1 ON T1.IDProducto = T0.IDProducto and T1.IDPlanPla = T0.IDPlaca
                    INNER JOIN PickeoProductoIngresado T2 ON T2.IDPProducto = T0.IDPProducto 
                    LEFT JOIN PlanificacionPlaca T3 ON T3.IDPlanPla = T0.IDPlaca
                    WHERE T0.IDPlaca = @id
                    AND T2.Cantidad != 0
                    GROUP BY 
                    T0.IDPProducto,
                    T0.IDProducto,
                    T0.Cantidad,
                    T1.SL1Code,
                    T1.SL2Code,
                    T1.SL3Code,
                    T1.SL4Code,
                    T2.IDPProducto,
                    T2.CargaEstado,
                    T2.Confirma,
                    T1.AbsEntry,
                    T3.CargaIncompleta,
                    T3.Confirmado";

                    var parameters = new { id };
                    var result = await connection.QueryAsync<ProductosContador>(query, parameters);
                    return Ok(result);
                }
            }




            
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerPaqueteriasProductoCarga(int idProducto, int idp) {
            using (var connection = new SqlConnection(_connectionString)) {
                string query = @"
                    SELECT 
                    IDPProducto,
                    Medidad,
                    Factor,
                    SUM(Cantidad) AS Cantidad,
                    COALESCE(Cargado,0) AS Cargado
                    FROM PickeoProductoIngresado 
                    WHERE 
                    IDPProducto = @idp
                    AND Cantidad != 0
                    GROUP BY 
                    IDPProducto,
                    Medidad,
                    Factor,
                    Cargado
                    ORDER BY Factor 
                    ";

                var parameters = new { idProducto, idp };
                var result = await connection.QueryAsync<ProductosCargar>(query, parameters);
                return Ok(result);
            }
        }

        [HttpPost]
        public async Task<IActionResult> guardarPaqueteriasCargada([FromBody] List<UpdateCargaProducto> registros) {
            using (var connection = new SqlConnection(_connectionString)) {
                await connection.OpenAsync();

                foreach (var registro in registros) {
                    string query = "UPDATE PickeoProductoIngresado SET Cargado = @cantidad, CargaEstado = 1 WHERE IDPProducto = @idpProducto AND Factor = @factor";

                    var parameters = new {
                        cantidad = registro.cantidad,
                        idpProducto = registro.idpProducto,
                        factor = registro.factor
                    };

                    await connection.ExecuteAsync(query, parameters);

                    if(registro.cantidad < registro.cantid) {

                        string updatePlanificacionQuery = @"
                            UPDATE PlanificacionPlaca 
                            SET CargaIncompleta = 1
                            WHERE IDPlanPla = @idPlan";

                        var parameterss = new { idPlan = registro.idplan };
                        var results = await connection.ExecuteAsync(updatePlanificacionQuery, parameterss);
                    }
                }
            }

            return Ok(new { message = "Datos guardados exitosamente." });
        }

        [HttpPost]
        public async Task<IActionResult> GuardarPaqueteriasConfirmada([FromBody] List<UpdateCargaProducto> registros) {
            using (var connection = new SqlConnection(_connectionString)) {
                await connection.OpenAsync();

                foreach (var registro in registros) {
                    string query = "UPDATE PickeoProductoIngresado SET Cargado = @cantidad, CargaEstado = 1, Confirma = 1 WHERE IDPProducto = @idpProducto AND Factor = @factor";

                    var parameters = new {
                        cantidad = registro.cantidad,
                        idpProducto = registro.idpProducto,
                        factor = registro.factor
                    };

                    await connection.ExecuteAsync(query, parameters);
                }
            }

            return Ok(new { message = "Datos guardados exitosamente." });
        }

        [HttpPost]
        public async Task<IActionResult> FinalizarCargaPlaca(int idPlan) {

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
                    await connection.OpenAsync();

                    string selectQuery = @"
                    SELECT COALESCE(CargaIncompleta, 0) AS Incon
                    FROM PlanificacionPlacaTransferencia 
                    WHERE IDPlanPla = @idPlan";
                    var parameters = new { idPlan };

                    var cargaIncompleta = await connection.QueryFirstOrDefaultAsync<int>(selectQuery, parameters);
                    Console.WriteLine("Carga" + cargaIncompleta);
                    if (cargaIncompleta == 0) {
                        string updatePlacaPedidoQuery = @"
                        UPDATE PlacaPedidoTransferencia
                        SET CantidadFinal = CantidadCargar, EstadoFinal = 1
                        WHERE IDPlanPla = @idPlan";
                        await connection.ExecuteAsync(updatePlacaPedidoQuery, parameters);

                        string updatePlanificacionPlacaQuery = @"
                        UPDATE PlanificacionPlacaTransferencia
                        SET Revision = 1, Cargado = 1, FechaFin= GETDATE()
                        WHERE IDPlanPla = @idPlan";
                        await connection.ExecuteAsync(updatePlanificacionPlacaQuery, parameters);
                    }

                    string updatePlanificacionQuery = @"
                    UPDATE PlanificacionPlacaTransferencia 
                    SET Cargado = 1, Revision = 1, FechaFin= GETDATE()
                    WHERE IDPlanPla = @idPlan";
                    var result = await connection.ExecuteAsync(updatePlanificacionQuery, parameters);

                    if (result > 0) {
                        return Ok(new { success = true, message = "Carga finalizada exitosamente." });
                    } else {
                        return NotFound(new { success = false, message = "No se encontraron registros para actualizar." });
                    }
                }


            } else {
                using (var connection = new SqlConnection(_connectionString)) {
                    await connection.OpenAsync();

                    string selectQuery = @"
                    SELECT COALESCE(CargaIncompleta, 0) AS Incon
                    FROM PlanificacionPlaca 
                    WHERE IDPlanPla = @idPlan";
                    var parameters = new { idPlan };

                    var cargaIncompleta = await connection.QueryFirstOrDefaultAsync<int>(selectQuery, parameters);
                    Console.WriteLine("Carga" + cargaIncompleta);
                    if (cargaIncompleta == 0) {
                        string updatePlacaPedidoQuery = @"
                        UPDATE PlacaPedido
                        SET CantidadFinal = CantidadCargar, EstadoFinal = 1
                        WHERE IDPlanPla = @idPlan";
                        await connection.ExecuteAsync(updatePlacaPedidoQuery, parameters);

                        string updatePlanificacionPlacaQuery = @"
                        UPDATE PlanificacionPlaca
                        SET Revision = 1, Cargado = 1, FechaFin= GETDATE()
                        WHERE IDPlanPla = @idPlan";
                        await connection.ExecuteAsync(updatePlanificacionPlacaQuery, parameters);
                    }

                    string updatePlanificacionQuery = @"
                    UPDATE PlanificacionPlaca 
                    SET Cargado = 1, Revision = 1, FechaFin= GETDATE()
                    WHERE IDPlanPla = @idPlan";
                    var result = await connection.ExecuteAsync(updatePlanificacionQuery, parameters);

                    if (result > 0) {
                        return Ok(new { success = true, message = "Carga finalizada exitosamente." });
                    } else {
                        return NotFound(new { success = false, message = "No se encontraron registros para actualizar." });
                    }
                }
            }










            
        }


        [HttpPost]
        public async Task<IActionResult> CargaIncompleta(int idPlan) {
            using (var connection = new SqlConnection(_connectionString)) {
                await connection.OpenAsync();
                string query = @"
                    UPDATE PlanificacionPlaca SET CargaIncompleta = 1 WHERE IDPlanPla = @idPlan";
                var parameters = new { idPlan };

                var result = await connection.ExecuteAsync(query, parameters);

                if (result > 0) {
                    return Ok(new { success = true, message = "Carga finalizado exitosamente." });
                } else {
                    return NotFound(new { success = false, message = "No se encontraron registros para actualizar." });
                }
            }
        }

        [HttpPost]
        public async Task<IActionResult> CargaConfirmada(int idPlan) {
            using (var connection = new SqlConnection(_connectionString)) {
                await connection.OpenAsync();
                string query = @"
                    UPDATE PlanificacionPlaca SET Confirmado = 1 WHERE IDPlanPla = @idPlan";
                var parameters = new { idPlan };

                var result = await connection.ExecuteAsync(query, parameters);

                if (result > 0) {
                    return Ok(new { success = true, message = "Carga finalizado exitosamente." });
                } else {
                    return NotFound(new { success = false, message = "No se encontraron registros para actualizar." });
                }
            }
        }

        [HttpPost]
        public async Task<IActionResult> AceptarCargaCompleta(int idPlan) {
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
                    await connection.OpenAsync();
                    string query = @"
                    UPDATE T0
                    SET T0.Cargado = (
                        SELECT SUM(P1.Cantidad)
                        FROM PickeoProductoIngresado P1
                        WHERE P1.IDPProducto = T0.IDPProducto
                        AND P1.Factor = T0.Factor
                    ),
                    T0.CargaEstado = 1
                    FROM PickeoProductoIngresado T0
                    INNER JOIN PickeoProducto T1 ON T1.IDPProducto = T0.IDPProducto
                    WHERE T1.IDPlaca = @idPlan
                    AND T0.Cargado IS NULL";
                    var parameters = new { idPlan };
                    var result = await connection.ExecuteAsync(query, parameters);

                    string querys = @"
                    UPDATE PlacaPedidoTransferencia SET CantidadFinal = CantidadCargar, EstadoFinal = 1 WHERE CantidadCargar = Cantidad AND IDPlanPla = @idPlan";

                    var results = await connection.ExecuteAsync(querys, parameters);

                    string selectQueryveri = @"
                    SELECT 
                        SUM(T0.CantidadCargar) AS TotalProducto, 
                        T0.IDProducto, 
                        (SELECT SUM(P1.Cargado) 
                         FROM PickeoProductoIngresado P1 
                         INNER JOIN PickeoProducto P2 
                         ON P2.IDPProducto = P1.IDPProducto 
                         WHERE P2.IDPlaca = T0.IDPlanPla 
                         AND P2.IDProducto = T0.IDProducto) AS Cargado
                    FROM PlacaPedidoTransferencia T0 
                    WHERE T0.CantidadFinal IS NULL 
                    AND T0.IDPlanPla = @idPlan 
                    GROUP BY T0.IDProducto, T0.IDPlanPla";

                    var resultados = await connection.QueryAsync(selectQueryveri, parameters);

                    foreach (var resultado in resultados) {
                        if (resultado.TotalProducto == resultado.Cargado) {
                            string updateQuery = @"
                    UPDATE PlacaPedidoTransferencia 
                    SET CantidadFinal = CantidadCargar , EstadoFinal = 1
                    WHERE IDProducto = @IDProducto 
                    AND IDPlanPla = @idPlan";

                            var updateParameters = new { IDProducto = resultado.IDProducto, idPlan };
                            await connection.ExecuteAsync(updateQuery, updateParameters);
                        }
                    }
                    string selectQuery = @"
                    SELECT COALESCE(CargaIncompleta, 0) AS Incon
                    FROM PlanificacionPlacaTransferencia 
                    WHERE IDPlanPla = @idPlan";

                    var cargaIncompleta = await connection.QueryFirstOrDefaultAsync<int>(selectQuery, parameters);
                    Console.WriteLine("Carga" + cargaIncompleta);
                    if (cargaIncompleta == 0) {
                        string updatePlacaPedidoQuery = @"
                        UPDATE PlacaPedidoTransferencia
                        SET CantidadFinal = CantidadCargar, EstadoFinal = 1
                        WHERE IDPlanPla = @idPlan";
                        await connection.ExecuteAsync(updatePlacaPedidoQuery, parameters);

                        string updatePlanificacionPlacaQuery = @"
                        UPDATE PlanificacionPlacaTransferencia
                        SET Revision = 1, Cargado = 1, FechaFin= GETDATE()
                        WHERE IDPlanPla = @idPlan";
                        await connection.ExecuteAsync(updatePlanificacionPlacaQuery, parameters);
                    }
                    if (result > 0) {
                        return Ok(new { success = true, message = "Carga confirmada exitosamente." });
                    } else {
                        return NotFound(new { success = false, message = "No se encontraron registros para actualizar." });
                    }
                }

            } else {
                using (var connection = new SqlConnection(_connectionString)) {
                    await connection.OpenAsync();
                    string query = @"
                    UPDATE T0
                    SET T0.Cargado = (
                        SELECT SUM(P1.Cantidad)
                        FROM PickeoProductoIngresado P1
                        WHERE P1.IDPProducto = T0.IDPProducto
                        AND P1.Factor = T0.Factor
                    ),
                    T0.CargaEstado = 1
                    FROM PickeoProductoIngresado T0
                    INNER JOIN PickeoProducto T1 ON T1.IDPProducto = T0.IDPProducto
                    WHERE T1.IDPlaca = @idPlan
                    AND T0.Cargado IS NULL";
                    var parameters = new { idPlan };
                    var result = await connection.ExecuteAsync(query, parameters);

                    string querys = @"
                    UPDATE PlacaPedido SET CantidadFinal = CantidadCargar, EstadoFinal = 1 WHERE CantidadCargar = Cantidad AND IDPlanPla = @idPlan";

                    var results = await connection.ExecuteAsync(querys, parameters);

                    string selectQueryveri = @"
                    SELECT 
                        SUM(T0.CantidadCargar) AS TotalProducto, 
                        T0.IDProducto, 
                        (SELECT SUM(P1.Cargado) 
                         FROM PickeoProductoIngresado P1 
                         INNER JOIN PickeoProducto P2 
                         ON P2.IDPProducto = P1.IDPProducto 
                         WHERE P2.IDPlaca = T0.IDPlanPla 
                         AND P2.IDProducto = T0.IDProducto) AS Cargado
                    FROM PlacaPedido T0 
                    WHERE T0.CantidadFinal IS NULL 
                    AND T0.IDPlanPla = @idPlan 
                    GROUP BY T0.IDProducto, T0.IDPlanPla";

                    var resultados = await connection.QueryAsync(selectQueryveri, parameters);

                    foreach (var resultado in resultados) {
                        if (resultado.TotalProducto == resultado.Cargado) {
                            string updateQuery = @"
                    UPDATE PlacaPedido 
                    SET CantidadFinal = CantidadCargar , EstadoFinal = 1
                    WHERE IDProducto = @IDProducto 
                    AND IDPlanPla = @idPlan";

                            var updateParameters = new { IDProducto = resultado.IDProducto, idPlan };
                            await connection.ExecuteAsync(updateQuery, updateParameters);
                        }
                    }
                    string selectQuery = @"
                    SELECT COALESCE(CargaIncompleta, 0) AS Incon
                    FROM PlanificacionPlaca 
                    WHERE IDPlanPla = @idPlan";

                    var cargaIncompleta = await connection.QueryFirstOrDefaultAsync<int>(selectQuery, parameters);
                    Console.WriteLine("Carga" + cargaIncompleta);
                    if (cargaIncompleta == 0) {
                        string updatePlacaPedidoQuery = @"
                        UPDATE PlacaPedido
                        SET CantidadFinal = CantidadCargar, EstadoFinal = 1
                        WHERE IDPlanPla = @idPlan";
                        await connection.ExecuteAsync(updatePlacaPedidoQuery, parameters);

                        string updatePlanificacionPlacaQuery = @"
                        UPDATE PlanificacionPlaca
                        SET Revision = 1, Cargado = 1, FechaFin= GETDATE()
                        WHERE IDPlanPla = @idPlan";
                        await connection.ExecuteAsync(updatePlanificacionPlacaQuery, parameters);
                    }
                    if (result > 0) {
                        return Ok(new { success = true, message = "Carga confirmada exitosamente." });
                    } else {
                        return NotFound(new { success = false, message = "No se encontraron registros para actualizar." });
                    }
                }
            }










            
        }

    }
}
