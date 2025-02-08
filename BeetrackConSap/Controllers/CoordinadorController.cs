using Microsoft.AspNetCore.Mvc;
using System.Data.SqlClient;
using Sap.Data.Hana;
using Dapper;
using System.Security.Claims;
using BeetrackConSap.Models;
using Newtonsoft.Json;
using System.Text;
using MorosidadWeb.Models;
using MySqlX.XDevAPI;
using System.Net.Http;
using NPOI.SS.Formula.Functions;
using Microsoft.Win32;


namespace BeetrackConSap.Controllers {
    public class CoordinadorController : Controller {

        private string usuario;
        private string clave;
        private readonly string _hanaConnectionString;
        private readonly string _connectionString;

        public CoordinadorController(IConfiguration configuration) {
            _connectionString = configuration.GetConnectionString("Femaco10");
            _hanaConnectionString = configuration.GetConnectionString("FemacoSap");
        }


        private void ObtenerUsuarioYClave() {
            string userDataJson = User.FindFirstValue(ClaimTypes.UserData);

            if (!string.IsNullOrEmpty(userDataJson)) {
                dynamic user = JsonConvert.DeserializeObject<dynamic>(userDataJson);
                usuario = user.usuario;
                clave = user.clave;
                Console.WriteLine("Usuario SAP: " + usuario);
                Console.WriteLine("Clave SAP: " + clave);
            } else {
                Console.WriteLine("No se encontraron datos de ClaimTypes.UserData.");
            }
        }

        public IActionResult Index() {
            return View("~/Views/Picking/Planificacion.cshtml");
        }
        public IActionResult CoordinadorVista() {
            return View("~/Views/Coordinador/CoordinadorVista.cshtml");
        }
        public IActionResult LastMileVista() {
            return View("~/Views/Coordinador/LastMileVista.cshtml");
        }
        public IActionResult ManifiestosAbiertos() {
            return View("~/Views/Coordinador/ManifiestosAbiertos.cshtml");
        }
        public IActionResult PickeadorConteo(int idpp) {
            ViewBag.IdPP = idpp;
            return View("~/Views/Picking/PickeadorConteo.cshtml");
        }
        public IActionResult CoorProdDetalles() {
            return View("~/Views/Coordinador/CoorProdDetalles.cshtml");
        }
        public IActionResult CoorCargaIncompleto() {
            return View("~/Views/Coordinador/CoorCargaIncompleto.cshtml");
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
                    COALESCE(T1.Enviado,0) AS Enviado,
                    COALESCE(T1.Cargar,0) AS Cargar,
                    COALESCE(T1.Cargado,0) AS Cargado,
                    COALESCE(T1.Revision,0) AS Revision,
                    COALESCE(T1.Sap,0) AS Sap
                    FROM Planificacion T0
                    INNER JOIN PlanificacionPlaca T1 ON T0.IDPlan = T1.IDPlan
                    LEFT JOIN PickeoProducto T2 ON T2.IDPlaca = T1.IDPlanPla
                    LEFT JOIN PickeoProducto T3 ON T3.IDPProducto = T2.IDPProducto AND T3.Finalizado = 1
                    GROUP BY T1.Placa, T1.IDPlanPla, T1.Enviado,T1.Cargar,T1.Cargado,T1.Revision,T1.Sap";

                var result = await connection.QueryAsync<PlacaPlan>(query);
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
                    T3.Descripcion,
                    T3.MedidaBase,
                    T3.Fabricante,
                    COALESCE(T4.Cantidad,0) AS Asignado,
                    (SELECT COALESCE(SUM(P1.Cantidad*P1.Factor),0) FROM PickeoProductoIngresado P1 WHERE P1.IDPProducto = T4.IDPProducto) AS Pickado,
                    COALESCE(T3.RevisadoCoor,0) AS Revisado,
                    T4.NuevaUbicacion,
                    T4.Reconteo,
                    T4.Finalizado
                    FROM Planificacion T0
                    INNER JOIN PlanificacionPlaca T1 ON T0.IDPlan = T1.IDPlan
                    INNER JOIN PlacaManifiesto T2 ON T1.IDPlanPla = T2.IDPlanPla
                    INNER JOIN PlacaPedido T3 ON T2.IDPlanMan = T3.IDPlanMan
                    LEFT JOIN PickeoProducto T4 ON T4.IDProducto = T3.IDProducto AND T4.IDPlaca = T1.IDPlanPla
                    LEFT JOIN PickeoProductoIngresado T5 ON T5.IDPProducto = T4.IDPProducto
                    WHERE 
                    T1.IDPlanPla = @IDPlan
                    GROUP BY 
                    T3.IDProducto,
                    T4.IDPProducto,
                    T3.Descripcion,
                    T3.MedidaBase,
                    T3.Fabricante,
                    T4.Cantidad,
                    T3.RevisadoCoor,
                    T4.NuevaUbicacion,
                    T4.Reconteo,
                    T4.Finalizado
                    ORDER BY Fabricante";

                var parameters = new { IDPlan = ID };
                var result = await connection.QueryAsync<CoordinadorProductos>(query, parameters);

                var updates = result.Where(r => r.Asignado == r.Pickado).ToList();

                foreach (var item in updates) {
                    var updateQuery = @"
                        UPDATE PlacaPedido
                        SET CantidadCargar = Cantidad, RevisadoCoor = 1 
                        WHERE IDProducto = @IDProducto AND IDPlanPla = @IDPlan"; 

                    var updateParams = new {
                        Pickado = item.Pickado,
                        IDProducto = item.IDProducto,
                        IDPlan = ID 
                    };
                    await connection.ExecuteAsync(updateQuery, updateParams);
                }

                return Ok(result);
            }
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerInformacionPedido(string numeroGuia) {
            using (var connection = new SqlConnection(_connectionString)) {
                string query = @"
                    SELECT 
                    IDProducto,
                    Descripcion,
                    MedidaBase,
                    Cantidad,
                    CONCAT(SL1Code,'-',SL2Code,'-',SL3Code,'-',SL4Code) AS Ubicacion
                    FROM PlacaPedido 
                    WHERE NumeroGuia = @numeroGuia
                    ";

                var parameters = new { numeroGuia };
                var result = await connection.QueryAsync<DetallePedido>(query, parameters);

                return Ok(result);
            }
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerProductosCargadaIncompleta(int ID) {
            using (var connection = new SqlConnection(_connectionString)) {
                string query = @"
                    SELECT 
                    T3.IDProducto,
                    SUM(T3.Cantidad) AS TotalCantidad,
                    T3.Descripcion,
                    T3.MedidaBase,
                    T3.Fabricante,
                    COALESCE(T4.Cantidad,0) AS Asignado,
                    (SELECT COALESCE(SUM(P1.Cantidad*P1.Factor),0) FROM PickeoProductoIngresado P1 WHERE P1.IDPProducto = T4.IDPProducto) AS Pickado,
                    (SELECT COALESCE(SUM(P2.Cargado*P2.Factor),0) FROM PickeoProductoIngresado P2 WHERE P2.IDPProducto = T4.IDPProducto AND P2.AbsEntry = T3.AbsEntry) AS Cargado,
                    (SELECT MIN(CAST(P2.EstadoFinal AS INT)) FROM PlacaPedido P2 WHERE P2.IDProducto = T3.IDProducto AND P2.IDPlanPla = T1.IDPlanPla ) AS Final,
                    COALESCE(T3.RevisadoCoor,0) AS Revisado
                    FROM Planificacion T0
                    INNER JOIN PlanificacionPlaca T1 ON T0.IDPlan = T1.IDPlan
                    INNER JOIN PlacaManifiesto T2 ON T1.IDPlanPla = T2.IDPlanPla
                    INNER JOIN PlacaPedido T3 ON T2.IDPlanMan = T3.IDPlanMan
                    LEFT JOIN PickeoProducto T4 ON T4.IDProducto = T3.IDProducto AND T4.IDPlaca = T1.IDPlanPla
                    LEFT JOIN PickeoProductoIngresado T5 ON T5.IDPProducto = T4.IDPProducto
                    WHERE 
                    T1.IDPlanPla = @IDPlan
                    GROUP BY 
                    T3.IDProducto,
                    T4.IDPProducto,
                    T3.Descripcion,
                    T3.MedidaBase,
                    T3.Fabricante,
                    T4.Cantidad,
                    T3.RevisadoCoor,
                    T1.IDPlanPla,
                    T3.AbsEntry
                    ORDER BY Fabricante";

                var parameters = new { IDPlan = ID };
                var result = await connection.QueryAsync<CoordinadorProductos>(query, parameters);

                var updates = result.Where(r => r.Pickado == r.Cargado).ToList();

                foreach (var item in updates) {
                    var updateQuery = @"
                        UPDATE PlacaPedido
                        SET CantidadFinal = CantidadCargar, EstadoFinal = 1 
                        WHERE IDProducto = @IDProducto 
                        AND IDPlanMan IN (SELECT IDPlanMan FROM PlacaManifiesto WHERE IDPLanPla = @IDPlan)";

                    var updateParams = new {
                        Cargado = item.Cargado,
                        IDProducto = item.IDProducto,
                        IDPlan = ID
                    };

                    await connection.ExecuteAsync(updateQuery, updateParams);
                }

                return Ok(result);
            }
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerManifiestoNumero(string numeroManifiesto) {
            HanaConnection hanaConnection = new(_hanaConnectionString);
            try {
                await hanaConnection.OpenAsync();

                string sapQuery = """
                    SELECT 
                        T1."U_EXP_PLACA",
                        T2."DocNum",
                        T2."CardName",
                        T2."Address",
                        T3."Phone1",
                        T3."CardCode",
                        T3."E_Mail",
                        T4."U_XM_LatitudS",
                        T4."U_XM_LongitudS",
                        T5."Dscription",
                        T5."ItemCode",
                        ROUND(T5."Price",2) AS "UnitPrice",
                        T5."Quantity",
                        T5."NumPerMsr",
                        T6."PickQtty",
                        T5."unitMsr"
                    FROM "@EXP_MANC" T0
                    INNER JOIN "@EXP_MAND" T1 ON T0."DocEntry" = T1."DocEntry"
                    INNER JOIN ORDR T2 ON T1."U_EXP_PEDENENTRY" = T2."DocEntry"
                    INNER JOIN OCRD T3 ON T2."CardCode" = T3."CardCode"
                    INNER JOIN RDR12 T4 ON T2."DocEntry"=T4."DocEntry"
                    INNER JOIN RDR1 T5 ON T2."DocEntry"=T5."DocEntry"
                    INNER JOIN PKL1 T6 ON T5."PickIdNo" = T6."AbsEntry" AND T2."DocEntry" = T6."OrderEntry" AND T5."LineNum" = T6."OrderLine"
                    WHERE T0."DocEntry" = '
                    """ + numeroManifiesto+"""
                    '
                    """;

                var manifiestos = await hanaConnection.QueryAsync<Manifiesto>(sapQuery);

                return Json(manifiestos);
            } catch (Exception ex) {
                Console.WriteLine($"Error: {ex.Message}");
                return Json(new { success = false, message = "Error al obtener el manifiesto." });
            } finally {
                await hanaConnection.CloseAsync();
            }
        }

        [HttpGet]
        public async Task<IActionResult> DetectarManifiestos(string idpick) {
            HanaConnection hanaConnection = new(_hanaConnectionString);
            try {
                await hanaConnection.OpenAsync();

                string sapQuery = """
                    SELECT 
                    T1."DocEntry",
                    T1."U_EXP_FECH",
                    T1."U_EXP_PLVE",
                    T1."U_EXP_TRAN",
                    T1."U_EXP_COND",
                    T1."U_EXP_UTES",
                    T1."U_EXP_CODSEDE",
                    T1."U_EXP_ESTA"
                    FROM "@EXP_MANLP" T0
                    INNER JOIN "@EXP_MANC" T1 ON T1."DocEntry" = T0."DocEntry"
                    WHERE T0."U_EXP_PKLO" = 
                    """ + idpick 
                    ;

                var manifiestos = await hanaConnection.QueryAsync<DetectarManifiesto>(sapQuery);

                return Json(manifiestos);
            } catch (Exception ex) {
                Console.WriteLine($"Error: {ex.Message}");
                return Json(new { success = false, message = "Error al obtener los manifiestos." });
            } finally {
                await hanaConnection.CloseAsync();
            }
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerManifiestoAbiertos(string fecha) {
            HanaConnection hanaConnection = new(_hanaConnectionString);
            try {
                await hanaConnection.OpenAsync();

                string sapQuery = """
                    SELECT 
                    "DocEntry",
                    "U_EXP_PLVE",
                    "U_EXP_TRAN",
                    "U_EXP_COND",
                    "U_EXP_QTYP",
                    "U_EXP_CODSEDE",
                    "U_EXP_FECH"
                    FROM "@EXP_MANC" T0
                    WHERE 
                    "U_EXP_ESTA" = 'O'
                    AND "U_EXP_FECH" = '
                    """+ fecha+"""
                    '
                    """;

                var manifiestos = await hanaConnection.QueryAsync<ManifiestosAbiertos>(sapQuery);

                return Json(manifiestos);
            } catch (Exception ex) {
                Console.WriteLine($"Error: {ex.Message}");
                return Json(new { success = false, message = "Error al obtener los manifiestos." });
            } finally {
                await hanaConnection.CloseAsync();
            }
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerPedidosProducto(int IDPlan, string IDProducto) {
            using (var connection = new SqlConnection(_connectionString)) {
                string query = @"
                    SELECT 
                    T2.IDProducto,
                    T2.Descripcion,
                    T2.Cantidad,
                    T2.NumeroGuia,
                    T2.IDPlanPed,
                    T2.Factor,
                    T2.CantidadBase,
                    T2.CantidadCargar/T2.Factor AS CantidadCargar
                    FROM PlanificacionPlaca T0
                    INNER JOIN PlacaManifiesto T1 ON T1.IDPlanPla = T0.IDPlanPla
                    INNER JOIN PlacaPedido T2 ON T2.IDPlanMan = T1.IDPlanMan
                    WHERE 
                    T0.IDPlanPla = @IDPlan
                    AND T2.IDProducto = @IDProducto
                    ORDER BY T2.Factor DESC, T2.Cantidad DESC
                    ";

                var parameters = new { IDPlan = IDPlan, IDProducto = IDProducto };
                var result = await connection.QueryAsync<PedidosProducto>(query, parameters);
                return Ok(result);
            }
        }


        [HttpPost]
        public async Task<IActionResult> ReconteoProducto(int idPlan, int idProducto) {
            using (var connection = new SqlConnection(_connectionString)) {
                await connection.OpenAsync();
                string query = @"
                    UPDATE PickeoProducto SET Finalizado = 0, Reconteo = 1 WHERE IDPlaca = @idPlan AND IDProducto = @idProducto";
                var parameters = new { idProducto, idPlan };

                var result = await connection.ExecuteAsync(query, parameters);

                if (result > 0) {
                    return Ok(new { success = true, message = "Enviado a reconteo exitosamente." });
                } else {
                    return NotFound(new { success = false, message = "No se encontraron registros para actualizar." });
                }
            }
        }

        [HttpPost]
        public async Task<IActionResult> GuardarCantidadPedido([FromBody] List<UpdateCantidadRequest> updates) {
            if (updates == null || !updates.Any()) {
                return BadRequest("No se proporcionaron actualizaciones.");
            }

            using (var connection = new SqlConnection(_connectionString)) {
                await connection.OpenAsync();
                using (var transaction = connection.BeginTransaction()) {
                    try {
                        foreach (var update in updates) {
                            if (update == null || string.IsNullOrEmpty(update.IdPlanPed)) {
                                continue; 
                            }

                            string query = @"
                                UPDATE PlacaPedido 
                                SET CantidadCargar = @cantidadCargar, RevisadoCoor = 1
                                WHERE IDPlanPed = @idPlanPed";

                            var parameters = new { cantidadCargar = update.CantidadCargar, idPlanPed = update.IdPlanPed };
                            await connection.ExecuteAsync(query, parameters, transaction);
                        }
                        transaction.Commit();
                        return Ok();
                    } catch (Exception ex) {
                        transaction.Rollback();
                        return StatusCode(500, "Error al guardar las cantidades: " + ex.Message);
                    }
                }
            }
        }

        [HttpGet]
public async Task<IActionResult> ValidarStockSap(int IDPlan)
{
    try
    {
        var connection = new SqlConnection(_connectionString);
        HanaConnection hanaConnection = new(_hanaConnectionString);

        // Intentando abrir las conexiones
        await hanaConnection.OpenAsync();
        Console.WriteLine("Conexión a SAP establecida");
        await connection.OpenAsync();
        Console.WriteLine("Conexión a SQL Server establecida");

        ObtenerUsuarioYClave(); // Verifica si usuario está correctamente asignado
        Console.WriteLine($"Usuario: {usuario}");
        string almacen = null;

        // Consulta SAP para obtener el almacén
        string sappQuery = $@"
            SELECT ""Warehouse"" FROM OUDG WHERE ""Code"" = '{usuario}'";
        using (var hanaCommand = new HanaCommand(sappQuery, hanaConnection))
        {
            using (var reader = await hanaCommand.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    almacen = reader["Warehouse"].ToString();
                    Console.WriteLine("Este es el almacén: " + almacen);
                }
                else
                {
                    Console.WriteLine("No se encontró el almacén para el usuario.");
                    return StatusCode(500, new { message = "No se encontró el almacén para el usuario" });
                }
            }
        }

        // Consulta SQL para obtener productos
        string sqlQuery = $@"
            SELECT IDProducto, Descripcion, SUM(CantidadFinal) AS Cantidad
            FROM PlacaPedido 
            WHERE IDPlanPla = {IDPlan}
            GROUP BY IDProducto, Descripcion";

        Console.WriteLine("SQLQUERY: " + sqlQuery);

        var productos = new List<(string idProducto, int cantidad)>();
        using (var sqlCommand = new SqlCommand(sqlQuery, connection))
        {
            using (var reader = await sqlCommand.ExecuteReaderAsync())
            {
                if (!reader.HasRows)
                {
                    Console.WriteLine("No se encontraron productos en la consulta SQL.");
                    return StatusCode(500, new { message = "No se encontraron productos" });
                }

                while (await reader.ReadAsync())
                {
                    int cantidad = 0;
                    if (int.TryParse(reader["Cantidad"].ToString(), out cantidad))
                    {
                        productos.Add((reader["IDProducto"].ToString(), cantidad));
                    }
                    else
                    {
                        Console.WriteLine("Error al convertir la cantidad del producto.");
                    }
                }
            }
        }

        // Mostramos los productos obtenidos
        Console.WriteLine("Productos obtenidos:");
        foreach (var producto in productos)
        {
            Console.WriteLine($"Producto: {producto.idProducto}, Cantidad: {producto.cantidad}");
        }

        var resultados = new List<object>();

        // Verificación del stock para cada producto
        foreach (var producto in productos)
        {
            string stockQuery = $@"
                SELECT ""OnHand""
                FROM OITW 
                WHERE ""ItemCode"" = '{producto.idProducto}' AND ""WhsCode"" = '{almacen}'";

            Console.WriteLine("STOCKQUERY: " + stockQuery);
            using (var hanaCommand = new HanaCommand(stockQuery, hanaConnection))
            {
                using (var reader = await hanaCommand.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        string stockDisponible = reader["OnHand"].ToString();

                        Console.WriteLine("STOCKDISPONIBLE: " + stockDisponible);
                        string[] partes = stockDisponible.Split('.');

                        decimal stockEntero = decimal.Parse(partes[0]);

                        Console.WriteLine("STOCKENTERO: " + stockEntero);
                        var resultadoProducto = new
                        {
                            IDProducto = producto.idProducto,
                            CantidadSolicitada = producto.cantidad,
                            StockDisponible = stockEntero
                        };

                        resultados.Add(resultadoProducto);

                        if (stockEntero < producto.cantidad)
                        {
                            string consultaDetalles = $@"
                                SELECT IDPlanPed, Cantidad, Factor 
                                FROM PlacaPedido 
                                WHERE IDPlanPla = {IDPlan} AND IDProducto = '{producto.idProducto}'";
                            Console.WriteLine("Consultando detalles de PlacaPedido...");
                            using (var sqlCommandDetalles = new SqlCommand(consultaDetalles, connection))
                            {
                                using (var readerDetalles = await sqlCommandDetalles.ExecuteReaderAsync())
                                {
                                    while (await readerDetalles.ReadAsync())
                                    {
                                        decimal cantidad = (decimal)readerDetalles["Cantidad"];
                                        decimal factor = (decimal)readerDetalles["Factor"];
                                        int idPlanPed = (int)readerDetalles["IDPlanPed"];

                                        if (cantidad < stockEntero)
                                        {
                                            stockEntero -= cantidad;
                                        }
                                        else
                                        {
                                            decimal cantidadNueva = Math.Floor(stockEntero / factor);
                                            int cantidadFinal = (int)cantidadNueva;

                                            string updateQuery = $@"
                                                UPDATE PlacaPedido 
                                                SET CantidadFinal = {cantidadFinal} 
                                                WHERE IDPlanPed = @IDPlanPed";
                                            Console.WriteLine("Actualizando cantidad en PlacaPedido...");
                                            using (var updateCommand = new SqlCommand(updateQuery, connection))
                                            {
                                                updateCommand.Parameters.AddWithValue("@IDPlanPed", idPlanPed);
                                                await updateCommand.ExecuteNonQueryAsync();
                                            }

                                            stockEntero -= cantidadFinal * (int)factor;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine($"No se encontró el stock para el producto {producto.idProducto}");
                        return StatusCode(500, new { message = $"No se encontró el stock para el producto {producto.idProducto}" });
                    }
                }
            }
        }

        // Si llegamos aquí, todo fue procesado correctamente
        Console.WriteLine("Stock validado correctamente para todos los productos.");
        return Ok(resultados);
    }
    catch (Exception ex)
    {
        // Captura información detallada del error
        string errorDetails = $"Message: {ex.Message}\nStackTrace: {ex.StackTrace}\nInnerException: {ex.InnerException?.Message}";

        // Devuelve el error completo en la respuesta
        Console.WriteLine($"Error al validar el stock SAP: {errorDetails}");
        return StatusCode(500, new { message = "Error al validar el stock SAP", error = errorDetails });
    }
}



        [HttpPost]
        public async Task<IActionResult> EnvioLastMile([FromBody] List<ManifiestoEnvio> manifiestos, string idPlan) {
            if (manifiestos == null || !manifiestos.Any()) {
                return Json(new { success = false, message = "No se recibieron datos válidos" });
            }

            try {
                var groupedManifiestos = manifiestos
                    .GroupBy(m => m.Documento)
                    .Select(g => new {
                        Documento = g.Key,
                        Dispatches = g.Select(m => new {
                            Identifier = m.Documento.ToString(),
                            ContactName = m.Cliente,
                            ContactAddress = m.Direccion,
                            ContactPhone = m.Telefono,
                            ContactId = m.CodigoCliente,
                            ContactEmail = m.Email,
                            Latitude = m.Latitud,
                            Longitude = m.Longitud,
                            Items = g.Select(item => new {
                                name = item.Unidad,
                                code = item.CodigoProducto,
                                unit_price = item.PrecioUnitario / item.CantidadxPaq,
                                quantity = item.Cantidad,
                                description = item.Descripcion
                            })
                            .DistinctBy(i => i.code)  
                            .ToList()
                        }).ToList()
                    }).ToList();

                var truckIdentifier = manifiestos.FirstOrDefault()?.Placa;
                if (truckIdentifier == null) {
                    return Json(new { success = false, message = "No se encontró una placa para el vehículo." });
                }

                var currentDate = System.DateTime.Now.ToString("dd-MM-yyyy");

                var requestData = new {
                    truck_identifier = truckIdentifier,
                    date = currentDate,
                    dispatches = groupedManifiestos.Select(g => new
                    {
                        identifier = g.Documento.ToString(),
                        contact_name = g.Dispatches.FirstOrDefault()?.ContactName,
                        contact_address = g.Dispatches.FirstOrDefault()?.ContactAddress,
                        contact_phone = g.Dispatches.FirstOrDefault()?.ContactPhone,
                        contact_id = g.Dispatches.FirstOrDefault()?.ContactId,
                        contact_email = g.Dispatches.FirstOrDefault()?.ContactEmail,
                        latitude = g.Dispatches.FirstOrDefault()?.Latitude,
                        longitude = g.Dispatches.FirstOrDefault()?.Longitude,
                        items = g.Dispatches.FirstOrDefault()?.Items
                    }).ToList()
                };

                var jsonContent = JsonConvert.SerializeObject(requestData);
                Console.WriteLine("JSON que se enviará en la solicitud POST:");
                Console.WriteLine(jsonContent);

                HttpClientHandler clientHandler = new HttpClientHandler();
                clientHandler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => { return true; };

                using (var httpClient = new HttpClient(clientHandler)) {
                    httpClient.DefaultRequestHeaders.Add("X-AUTH-TOKEN", "775b400c6f1887ecae6ecef2c3998ba6749ac279687727edceb0ed84b5c34ef6");

                    var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                    var response = await httpClient.PostAsync("https://femaco.dispatchtrack.com/api/external/v1/routes", httpContent);

                    if (response.IsSuccessStatusCode) {
                        var responseContent = await response.Content.ReadAsStringAsync();

                        Console.WriteLine("Respuesta JSON recibida: ");
                        Console.WriteLine(responseContent);

                        var result = JsonConvert.DeserializeObject<LastMileResponse>(responseContent);
                        var routeId = result.response.route_id;
                        using (var connection = new SqlConnection(_connectionString)) {
                            await connection.OpenAsync();
                            string query = $@"
                                UPDATE PlanificacionPlaca 
                                SET LastMileCodigo = {routeId}
                                WHERE IDPlanPla = {idPlan}";

                            Console.WriteLine(query);
                            Console.WriteLine(routeId);
                            Console.WriteLine(idPlan);

                            var command = new SqlCommand(query, connection);

                            var results = await command.ExecuteNonQueryAsync();
                            if (results > 0) {
                                Console.WriteLine("La actualización fue exitosa.");
                            } else {
                                Console.WriteLine("No se encontró ningún registro para actualizar.");
                            }
                        }

                        return Json(new { success = true, message = "Datos enviados a LastMile", data = responseContent });
                    } else {
                        var responseContent = await response.Content.ReadAsStringAsync();
                        dynamic jsonResponse = JsonConvert.DeserializeObject(responseContent);
                        string errorMessage = jsonResponse.error.message.value;
                        Console.WriteLine($"Error en la solicitud: {errorMessage}");

                        return Json(new { success = false, message = errorMessage });
                    }
                }


            } catch (Exception ex) {
                return Json(new { success = false, message = $"Ocurrió un error: {ex.Message}" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> GuardarCantidadFinal([FromBody] List<UpdateCantidadRequest> updates) {
            if (updates == null || !updates.Any()) {
                return BadRequest("No se proporcionaron actualizaciones.");
            }

            using (var connection = new SqlConnection(_connectionString)) {
                await connection.OpenAsync();
                using (var transaction = connection.BeginTransaction()) {
                    try {
                        foreach (var update in updates) {
                            if (update == null || string.IsNullOrEmpty(update.IdPlanPed)) {
                                continue;
                            }

                            string query = @"
                                UPDATE PlacaPedido 
                                SET CantidadFinal = @cantidadCargar, EstadoFinal = 1
                                WHERE IDPlanPed = @idPlanPed";

                            var parameters = new { cantidadCargar = update.CantidadCargar, idPlanPed = update.IdPlanPed };
                            await connection.ExecuteAsync(query, parameters, transaction);
                        }
                        transaction.Commit();
                        return Ok();
                    } catch (Exception ex) {
                        transaction.Rollback();
                        return StatusCode(500, "Error al guardar las cantidades: " + ex.Message);
                    }
                }
            }
        }

        [HttpPost]
        public async Task<IActionResult> GuardarCantidadFinalConfirma([FromBody] List<UpdateCantidadRequest> updates) {
            if (updates == null || !updates.Any()) {
                return BadRequest("No se proporcionaron actualizaciones.");
            }

            using (var connection = new SqlConnection(_connectionString)) {
                await connection.OpenAsync();

                using (var transaction = connection.BeginTransaction()) {
                    try {
                        foreach (var update in updates) {
                            if (update == null || string.IsNullOrEmpty(update.IdPlanPed)) {
                                continue;
                            }

                            string query = @"
                                UPDATE PlacaPedido 
                                SET CantidadFinal = @cantidadCargar, EstadoFinal = 1
                                WHERE IDPlanPed = @idPlanPed";

                            var parameters = new { cantidadCargar = update.CantidadCargar, idPlanPed = update.IdPlanPed };


                            await connection.ExecuteAsync(query, parameters, transaction);

                            string querys = @"
                                UPDATE PickeoProductoIngresado SET Confirma = 1 WHERE IDPProducto = @idp";

                            var parameterss = new { idp = update.Idp };
                            await connection.ExecuteAsync(querys, parameterss, transaction);
                        }
                        transaction.Commit();
                        return Ok();
                    } catch (Exception ex) {
                        transaction.Rollback();
                        return StatusCode(500, "Error al guardar las cantidades: " + ex.Message);
                    }
                }
            }
        }

        [HttpPost]
        public async Task<IActionResult> FinalizarCargadaPlaca(int idPlan) {
            using (var connection = new SqlConnection(_connectionString)) {
                await connection.OpenAsync();
                string query = @"
                    UPDATE PlanificacionPlaca SET Cargar = 1 WHERE IDPlanPla = @idPlan";
                var parameters = new {idPlan };

                var result = await connection.ExecuteAsync(query, parameters);

                if (result > 0) {
                    return Ok(new { success = true, message = "Enviado finalizado exitosamente." });
                } else {
                    return NotFound(new { success = false, message = "No se encontraron registros para actualizar." });
                }
            }
        }

        [HttpPost]
        public async Task<IActionResult> FinalizarRevisionPlaca(int idPlan) {
            using (var connection = new SqlConnection(_connectionString)) {
                await connection.OpenAsync();
                string query = @"
                    UPDATE PlanificacionPlaca SET Revision = 1 WHERE IDPlanPla = @idPlan";
                var parameters = new { idPlan };

                var result = await connection.ExecuteAsync(query, parameters);

                if (result > 0) {
                    return Ok(new { success = true, message = "Enviado finalizado exitosamente." });
                } else {
                    return NotFound(new { success = false, message = "No se encontraron registros para actualizar." });
                }
            }
        }


        public async Task<IActionResult> ObtenerDatosYEnviar(int IDPlan) {
            string sessionId = null;
            int absoluteEntry = 0;

            bool success = true;

            string almacen = null;
            string usuario = null;
            string clave = null;

            using (var connection = new SqlConnection(_connectionString)) {
                await connection.OpenAsync();
                //retorna almacen y usuario
                var infoplan = @"
                    SELECT T0.Almacen, T0.Usuario, T0.Clave FROM Planificacion T0
                    INNER JOIN PlanificacionPlaca T1 ON T1.IDPlan = T0.IDPlan
                    WHERE T1.IDPlanPla = @IDPlan";

                var infoplanQueryParam = new { IDPlan = IDPlan };
                var infoplanResult = await connection.QueryFirstOrDefaultAsync(infoplan, infoplanQueryParam);

                almacen = infoplanResult.Almacen;
                usuario = infoplanResult.Usuario;
                clave = infoplanResult.Clave;
                

                var query = @"
                    SELECT T0.IDPlanMan, T0.DocNum, T0.LineNum, T0.CantidadFinal, T2.Placa, T0.AbsEntry, CASE WHEN T0.CantidadFinal/T0.Factor < T0.CantidadBase THEN T0.CantidadFinal/T0.Factor ELSE T0.CantidadBase END AS CantidadBase
                    FROM PlacaPedido T0
                    INNER JOIN PlacaManifiesto T1 ON T1.IDPlanMan = T0.IDPlanMan
                    INNER JOIN PlanificacionPlaca T2 ON T2.IDPlanPla = T1.IDPlanPla
                    WHERE T1.IDPlanPla = @IDPlan AND T0.CantidadFinal != 0 AND T0.EstadoFinal = 1
                    ORDER BY T0.IDPlanMan ASC";

                var updatequery = @"
                    UPDATE PlanificacionPlaca SET Sap = 1 WHERE IDPlanPla = @IDPlan";

                var uptparam = new { IDPlan = IDPlan};
                await connection.ExecuteAsync(updatequery, uptparam);

                var command = new SqlCommand(query, connection);
                command.Parameters.AddWithValue("@IDPlan", IDPlan);
                var reader = await command.ExecuteReaderAsync();

                var groups = new Dictionary<int, List<dynamic>>();

                while (await reader.ReadAsync()) {
                    var docNum = reader["DocNum"];
                    var lineNumStr = reader["LineNum"].ToString(); 
                    int lineNum = 0;

                    if (!int.TryParse(lineNumStr, out lineNum)) {
                        Console.WriteLine($"Advertencia: No se pudo convertir 'LineNum' en {lineNumStr}");
                        continue;  
                    }

                    var cantidadFinal = (int)reader["CantidadFinal"];
                    //var cantidadBase = (int)reader["CantidadBase"];
                    var cantidadBaseDecimal = (decimal)reader["CantidadBase"];
                    int cantidadBase = (int)cantidadBaseDecimal;
                    var placa = reader["Placa"]?.ToString();
                    var absEntry = reader["AbsEntry"].ToString();
                    var idPlanMan = (int)reader["IDPlanMan"]; 

                    if (!groups.ContainsKey(idPlanMan)) {
                        groups[idPlanMan] = new List<dynamic>();
                    }

                    groups[idPlanMan].Add(new {
                        DocNum = docNum,
                        LineNum = lineNum,
                        CantidadFinal = cantidadFinal,
                        CantidadBase = cantidadBase,
                        Placa = placa,
                        AbsEntry = absEntry,
                        IDPlanMan = idPlanMan
                    });
                }

                await reader.CloseAsync();

                if (groups.Count == 0) {
                    return BadRequest("No se encontraron registros.");
                }

                using (var httpClient = new HttpClient(new HttpClientHandler {
                    ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
                })) {
                    var jsonLogin = JsonConvert.SerializeObject(new {
                        CompanyDB = "FEMACO_PROD",
                        Password = clave,
                        UserName = usuario
                    });
                    var loginContent = new StringContent(jsonLogin, Encoding.UTF8, "application/json");
                    var loginResponse = await httpClient.PostAsync("https://192.168.1.9:50000/b1s/v1/Login", loginContent);

                    if (loginResponse.IsSuccessStatusCode) {
                        var responseContent = await loginResponse.Content.ReadAsStringAsync();
                        dynamic responseObject = JsonConvert.DeserializeObject(responseContent);
                        sessionId = responseObject.SessionId;
                        Console.WriteLine("Logueado correctamente.");
                    } else {
                        return BadRequest($"Error en la solicitud de login: {loginResponse.StatusCode}");
                    }

                    foreach (var group in groups) {
                        var groupId = group.Key; 
                        var groupData = group.Value; 

                        var pickListsLinesGroup = new List<object>();
                        var absEntriesGroup = new List<string>();
                        string placaGroup = groupData.First().Placa;

                        int lineNumber = 0;

                        foreach (var line in groupData) {
                            pickListsLinesGroup.Add(new {
                                BaseObjectType = 17,
                                LineNumber = lineNumber,
                                OrderEntry = line.DocNum,
                                OrderRowID = line.LineNum,
                                ReleasedQuantity = line.CantidadBase  
                            });
                            absEntriesGroup.Add(line.AbsEntry);

                            lineNumber++;
                        }

                        var jsonBody = new {
                            ObjectType = "156",
                            PickDate = DateTime.Now.ToString("yyyy-MM-dd"),
                            U_SEDE = almacen,
                            U_EXF_PLC = placaGroup,
                            PickListsLines = pickListsLinesGroup
                        };

                        string jsonToSend = JsonConvert.SerializeObject(jsonBody, Formatting.Indented);
                        Console.WriteLine("JSON a enviar para IDPlanMan " + groupId + ":");
                        Console.WriteLine(jsonToSend);

                        var jsonContent = new StringContent(jsonToSend, Encoding.UTF8, "application/json");


                        httpClient.DefaultRequestHeaders.Add("Cookie", "B1SESSION=" + sessionId);
                        var postResponse = await httpClient.PostAsync("https://192.168.1.9:50000/b1s/v1/PickLists", jsonContent);

                        if (postResponse.IsSuccessStatusCode) {
                            var postResponseContent = await postResponse.Content.ReadAsStringAsync();
                            Console.WriteLine("POST subido correctamente para IDPlanMan " + groupId);
                            dynamic postResponseJson = JsonConvert.DeserializeObject(postResponseContent);
                            absoluteEntry = postResponseJson.Absoluteentry;

                            var patchLines = new List<object>();
                            lineNumber = 0;

                            foreach (var line in groupData.Select((value, index) => new { value, index })) {
                                var cantidadFinal = (int)line.value.CantidadFinal;
                                var binAbsEntry = absEntriesGroup[line.index];

                                patchLines.Add(new {
                                    AbsoluteEntry = absoluteEntry,
                                    LineNumber = lineNumber,
                                    OrderEntry = line.value.DocNum,
                                    OrderRowID = line.value.LineNum,
                                    DocumentLinesBinAllocations = new[] {
                                        new {
                                            BinAbsEntry = binAbsEntry,
                                            Quantity = cantidadFinal, 
                                            AllowNegativeQuantity = "tNO",
                                            SerialAndBatchNumbersBaseLine = -1,
                                            BaseLineNumber = lineNumber
                                        }
                                    }
                                });

                                lineNumber++;
                            }

                            var patchBody = new { PickListsLines = patchLines };
                            string patchJson = JsonConvert.SerializeObject(patchBody, Formatting.Indented);
                            Console.WriteLine("JSON para PATCH para IDPlanMan " + groupId + ":");
                            Console.WriteLine(patchJson);

                            var patchContent = new StringContent(patchJson, Encoding.UTF8, "application/json");
                            var patchResponse = await httpClient.PatchAsync($"https://192.168.1.9:50000/b1s/v1/PickLists({absoluteEntry})", patchContent);

                            if (patchResponse.IsSuccessStatusCode) {
                                Console.WriteLine("PATCH subido correctamente para IDPlanMan " + groupId);
                                string updateQuery = @"
                                UPDATE PlacaManifiesto 
                                SET IDPick = @AbsoluteEntry
                                WHERE IDPlanMan = @IDPlanMan";

                                var updateParams = new {
                                    AbsoluteEntry = absoluteEntry,
                                    IDPlanMan = groupId  
                                };
                                await connection.ExecuteAsync(updateQuery, updateParams);
                            } else {
                                var patchResponseContent = await patchResponse.Content.ReadAsStringAsync();
                                success = false;
                                var errorMessage = $"Error al actualizar datos para IDPlanMan {groupId}: {patchResponse.StatusCode} - {patchResponseContent}";
                                return Json(new { success = false, message = errorMessage });
                            }

                        } else {

                            var updatequerys = @"
                                UPDATE PlanificacionPlaca SET Sap = null WHERE IDPlanPla = @IDPlan";

                            var uptparams = new { IDPlan = IDPlan };
                            await connection.ExecuteAsync(updatequerys, uptparams);

                            var postResponseContent = await postResponse.Content.ReadAsStringAsync();
                            success = false;
                            Console.WriteLine($"Error al enviar datos para IDPlanMan {groupId}: {postResponse.StatusCode} - {postResponseContent}");
                            return StatusCode(500, $"Error en POST: {postResponseContent}");
                        }
                    }
                    if (success) {
                        return Ok(new { success = true, message = "Datos enviados y actualizados correctamente: " + absoluteEntry });
                    } else {
                        return StatusCode(500, "Hubo un error en el proceso de POST o PATCH.");
                    }
                }
            }
        }
    }
}
