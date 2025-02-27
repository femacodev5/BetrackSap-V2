using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using MorosidadWeb.Models;
using BeetrackConSap.Models;
using Sap.Data.Hana;
using System.Data;
using MySql.Data.MySqlClient;
using SAPbobsCOM;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using NPOI.HSSF.UserModel;
using MorosidadWeb.Models.ViewModels;
using Newtonsoft.Json;
using System.Dynamic;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Data.SqlClient;
using System.Text;
using System.Net.Http;
using MySqlX.XDevAPI.Common;
using BeetrackConSap.Models;
using static NPOI.HSSF.Util.HSSFColor;
using System.Numerics;
using Mysqlx.Crud;
using Microsoft.Win32;
using static iText.StyledXmlParser.Jsoup.Select.Evaluator;
using static iText.StyledXmlParser.Jsoup.Select.NodeFilter;

namespace MorosidadWeb.Controllers {
    [Authorize]
    public class HomeController : Controller {
        private string usuario;
        private string clave;

        private readonly string _hanaConnectionString;
        private readonly string _connectionString;

        private Company _company;
        private int error;
        public HomeController(IConfiguration configuration) {
            _connectionString = configuration.GetConnectionString("Femaco10");
            _hanaConnectionString = configuration.GetConnectionString("FemacoSap");
        }
        public IActionResult Index() {
            ObtenerUsuarioYClave();

            List<SelectListItem> territorios = ObtenerTerritorios();
            List<SelectListItem> vendedores = ObtenerVendedores();
            List<SelectListItem> zonarep = ObtenerZonarep();

            ViewBag.Territorios = territorios;
            ViewBag.Vendedores = vendedores;
            ViewBag.Zonarep = zonarep;

            return View("~/Views/Home/Consultas.cshtml");
            //return View();
        }

        public IActionResult ConsultasEnviadas() {
            return View("~/Views/Home/ConsultasEnviadas.cshtml");
        }
        public IActionResult ConsultasHistorial() {
            return View("~/Views/Home/ConsultasHistorial.cshtml");
        }
        public IActionResult Consultas() {
            ObtenerUsuarioYClave();

            List<SelectListItem> territorios = ObtenerTerritorios();
            List<SelectListItem> vendedores = ObtenerVendedores();
            List<SelectListItem> zonarep = ObtenerZonarep();

            ViewBag.Territorios = territorios;
            ViewBag.Vendedores = vendedores;
            ViewBag.Zonarep = zonarep;
            return View("~/Views/Home/Consultas.cshtml");
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
        public IActionResult LeerCodigoPrueba() {
            return View("~/Views/Prueba/LeerCodigoPrueba.cshtml");
        }
        public record MorosidadDTO(int Id, string Detalle, bool Aprobado);

        public List<SelectListItem> ObtenerTerritorios() {
            List<SelectListItem> territorios = new List<SelectListItem>();
            HanaConnection hanaConnection = new(_hanaConnectionString);
            hanaConnection.Open();
            if (hanaConnection.State == ConnectionState.Open) {
                string query = """SELECT "descript" FROM OTER""";
                using (var command = hanaConnection.CreateCommand()) {
                    command.CommandText = query;
                    using (var reader = command.ExecuteReader()) {
                        while (reader.Read()) {
                            string territorio = reader["descript"].ToString();
                            territorios.Add(new SelectListItem {
                                Value = territorio,
                                Text = territorio
                            });
                        }
                    }
                }
                hanaConnection.Close();
            }
            return territorios;
        }
        public List<SelectListItem> ObtenerVendedores() {
            List<SelectListItem> vendedores = new List<SelectListItem>();
            HanaConnection hanaConnection = new(_hanaConnectionString);
            hanaConnection.Open();
            if (hanaConnection.State == ConnectionState.Open) {
                string query = """SELECT "SlpCode", "SlpName" FROM OSLP WHERE "Active" = 'Y' AND "U_FEM_FECFIN" > CURRENT_DATE""";
                using (var command = hanaConnection.CreateCommand()) {
                    command.CommandText = query;
                    using (var reader = command.ExecuteReader()) {
                        while (reader.Read()) {
                            string slpCode = reader["SlpCode"].ToString();
                            string slpName = reader["SlpName"].ToString();
                            vendedores.Add(new SelectListItem {
                                Value = slpCode,
                                Text = slpName
                            });
                        }
                    }
                }
                hanaConnection.Close();
            }
            return vendedores;
        }
        public List<SelectListItem> ObtenerZonarep() {
            List<SelectListItem> zonarep = new List<SelectListItem>();
            HanaConnection hanaConnection = new(_hanaConnectionString);
            hanaConnection.Open();
            if (hanaConnection.State == ConnectionState.Open) {
                string query = """SELECT "Code","Name" FROM "@EXF_ZONRPT" """;
                using (var command = hanaConnection.CreateCommand()) {
                    command.CommandText = query;
                    using (var reader = command.ExecuteReader()) {
                        while (reader.Read()) {
                            string Code = reader["Code"].ToString();
                            string Name = reader["Name"].ToString();
                            zonarep.Add(new SelectListItem {
                                Value = Code,
                                Text = Name
                            });
                        }
                    }
                }
                hanaConnection.Close();
            }
            return zonarep;
        }
        public async Task<IActionResult> ObtenerProgramaciones(string fechaprog) {
            string connectionString = "Server=192.168.1.10;Database=APPWEBFORSAP;User Id=sa;Password=f3m4c05rl;";
            string fechaInicio = DateTime.ParseExact(fechaprog, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture).ToString("yyyyMMdd");

            string query = @"SELECT T0.PKID as IDPro, T0.Placa, T0.Fecha
                                (SELECT TOP 1 T2.Fecha 
                                 FROM DiasReparto T2 
                                 WHERE T2.ID_Programaciones = T0.PKID 
                                 ORDER BY T2.Fecha ASC) AS FechaInicio, 
                                STUFF((SELECT ',''' + CONVERT(VARCHAR(10), T1.PKID) + '''' 
                                       FROM Programaciones T3 
                                       INNER JOIN ZonasReparto T1 ON T1.ID_Programaciones = T3.PKID 
                                       WHERE T1.ID_Programaciones = T0.PKID 
                                       FOR XML PATH('')), 1, 1, '') AS ZonasRep 
                        FROM Programaciones T0 
                        WHERE (SELECT TOP 1 T2.Fecha 
                               FROM DiasReparto T2 
                               WHERE T2.ID_Programaciones = T0.PKID 
                               ORDER BY T2.Fecha ASC) = @FechaInicio";

            using (SqlConnection sqlConnection = new SqlConnection(connectionString)) {
                sqlConnection.Open();
                IEnumerable<Programacion> programaciones = await sqlConnection.QueryAsync<Programacion>(query, new { FechaInicio = fechaInicio });
                return Ok(programaciones);
            }
        }
        public async Task<IActionResult> VentaConConsolidado(string hastavenc, string terri, string rpt, string znrpt) {
            string text = "" + rpt + "";
            string textzn = "" + znrpt + "";
            string territorio = "" + terri + "";
            int length = text.Length;
            int lengthzn = textzn.Length;
            int lenterri = territorio.Length;
            var strterri = """

                """;
            if (lenterri > 0) {
                strterri = """
                        AND T3."U_XM_TerritorioS" IN (
                    """
                        + terri +
                        """
                    ) 
                    """;

            }
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

            if (lengthzn == 0) {
                if (length == 0) {
                    var str = """
                SELECT
                T1."DocNum" AS "NDOCUMENTO",
                T1."DocEntry" AS "ENTRY",
                T2."LineNum" AS "POSICI",
                SUBSTRING(T3."U_XM_LatitudS",0,10) AS "LATITUD",
                SUBSTRING(T3."U_XM_LongitudS",0,10) AS "LONGITUD",
                T3."StreetS" AS "DIRECCION",
                T2."Dscription" AS "NOMBREITEM",
                T2."OpenCreQty" AS "CANTIDAD",
                T2."ItemCode" AS "CODIGOITEM",
                TO_VARCHAR (TO_DATE(T2."ShipDate"), 'DD/MM/YYYY') AS "FECHAMINENTREGA",
                TO_VARCHAR (TO_DATE(T1."DocDueDate"), 'DD/MM/YYYY') AS "FECHAMAXENTREGA",
                '07:30' AS "MINVENTANAHORARIA1",
                '20:30' AS "MAXVENTANAHORARIA1",
                CASE 
                    WHEN COALESCE(T2."Weight1",0) = 0 THEN 0
                    ELSE ROUND((T2."PriceBefDi"/T2."Weight1") ,4)
                END AS "COSTOXKILO",
                T2."GrssProfit" AS "COSTOITEM",
                T2."Weight1" AS "CAPACIDADUNO",
                T4."LicTradNum" AS "IDENTIFICADORCONTACTO",
                T4."CardName" AS "NOMBRECONTACTO",
                T4."Phone1" AS "TELEFONO",
                '' AS "EMAILCONTACTO",
                CASE 
                    WHEN T3."U_XM_TerritorioS" = 'T. MOQUEGUA' THEN 'MQGCNT'
                    WHEN T3."U_XM_TerritorioS" = 'T. CUSCO' THEN 'ALMCUSCO'
                    ELSE 'FEMACO'
                END AS "CTORIGEN",
                T5."SlpName" AS "VENDEDOR",
                T3."U_EXF_ZONRPTS" AS "Name",
                T2."NumPerMsr" AS "Factor",
                T1."Weight" AS "PESOTOTAL",
                T1."GrosProfit" AS "UTILIDADTOTAL",
                T2."GrssProfit" AS "UTILIDAD",
                T7."OnHand" AS "STOCK",
                CASE 
                WHEN T7."OnHand" < (T2."OpenCreQty" * T2."NumPerMsr") THEN 1 ELSE 0
                END AS "ALERTASTOCK",
                T8."InvntryUom" AS "MEDIDABASE"

                FROM ORDR T1
                INNER JOIN RDR1 T2 ON T1."DocEntry"=T2."DocEntry"
                INNER JOIN RDR12 T3 ON T1."DocEntry"=T3."DocEntry"
                INNER JOIN OCRD T4 ON T1."CardCode"=T4."CardCode"
                INNER JOIN OSLP T5 ON T1."SlpCode"=T5."SlpCode"
                LEFT OUTER JOIN "@EXF_ZONRPT" T6 ON T3."U_EXF_ZONRPTS" = T6."Name"
                INNER JOIN OITW T7 ON T7."ItemCode" = T2."ItemCode" AND T7."WhsCode" = T2."WhsCode"
                INNER JOIN OITM T8 ON T8."ItemCode" = T2."ItemCode"

                WHERE
                T1."DocStatus" = 'O'
                AND T2."Quantity"<>0
                AND T2."WhsCode" = '
                """
                + almacen +
                """
                '
                AND T2."OpenCreQty" != 0
                AND T2."PickStatus" = 'N'
                AND T1."U_FEM_ESTIENDA" = 'N'
                AND T3."U_XM_TerritorioS" != 'Territorio A'
                AND T3."U_EXF_ZONRPTS" != '99'
                AND T3."U_XM_LatitudS" IS NOT NULL 
                AND T3."U_XM_LongitudS" IS NOT NULL 
                AND T3."StreetS"<>'FISCAL'
                AND T1."DocDueDate" <= '
                """
                + hastavenc +
                """
                '
                """
                + strterri +
                """ 
                ORDER BY T1."DocDueDate", T1."DocNum", CASE WHEN T7."OnHand" < (T2."OpenCreQty" * T2."NumPerMsr") THEN 1 ELSE 0 END DESC;
                """;

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
                        item.Almacen = almacen;
                    }
                    ventaConConsolidadoSap = ventaConConsolidadoSap.Where(item => item.Existe != 2).ToList();

                    await connection.CloseAsync();

                    return Ok(ventaConConsolidadoSap);

                } else {
                    Console.WriteLine("ACA ESTOY");
                    var str = """
                SELECT
                T1."DocNum" AS "NDOCUMENTO",
                T1."DocEntry" AS "ENTRY",
                T2."LineNum" AS "POSICI",
                SUBSTRING(T3."U_XM_LatitudS",0,10) AS "LATITUD",
                SUBSTRING(T3."U_XM_LongitudS",0,10) AS "LONGITUD",
                T3."StreetS" AS "DIRECCION",
                T2."Dscription" AS "NOMBREITEM",
                T2."OpenCreQty" AS "CANTIDAD",
                T2."ItemCode" AS "CODIGOITEM",
                TO_VARCHAR (TO_DATE(T2."ShipDate"), 'DD/MM/YYYY') AS "FECHAMINENTREGA",
                TO_VARCHAR (TO_DATE(T1."DocDueDate"), 'DD/MM/YYYY') AS "FECHAMAXENTREGA",
                '07:30' AS "MINVENTANAHORARIA1",
                '20:30' AS "MAXVENTANAHORARIA1",
                CASE 
                    WHEN COALESCE(T2."Weight1",0) = 0 THEN 0
                    ELSE ROUND((T2."PriceBefDi"/T2."Weight1") ,4)
                END AS "COSTOXKILO",
                T2."GrssProfit" AS "COSTOITEM",
                T2."Weight1" AS "CAPACIDADUNO",
                T4."LicTradNum" AS "IDENTIFICADORCONTACTO",
                T4."CardName" AS "NOMBRECONTACTO",
                T4."Phone1" AS "TELEFONO",
                '' AS "EMAILCONTACTO",
                CASE 
                    WHEN T3."U_XM_TerritorioS" = 'T. MOQUEGUA' THEN 'MQGCNT'
                    WHEN T3."U_XM_TerritorioS" = 'T. CUSCO' THEN 'ALMCUSCO'
                    ELSE 'FEMACO'
                END AS "CTORIGEN",
                T5."SlpName" AS "VENDEDOR",
                T3."U_EXF_ZONRPTS" AS "Name",
                T2."NumPerMsr" AS "Factor",
                T1."Weight" AS "PESOTOTAL",
                T1."GrosProfit" AS "UTILIDADTOTAL",
                T2."GrssProfit" AS "UTILIDAD",
                T7."OnHand" AS "STOCK",
                CASE 
                WHEN T7."OnHand" < (T2."OpenCreQty" * T2."NumPerMsr") THEN 1 ELSE 0
                END AS "ALERTASTOCK",
                T8."InvntryUom" AS "MEDIDABASE"
                FROM ORDR T1
                INNER JOIN RDR1 T2 ON T1."DocEntry"=T2."DocEntry"
                INNER JOIN RDR12 T3 ON T1."DocEntry"=T3."DocEntry"
                INNER JOIN OCRD T4 ON T1."CardCode"=T4."CardCode"
                INNER JOIN OSLP T5 ON T1."SlpCode"=T5."SlpCode"
                LEFT OUTER JOIN "@EXF_ZONRPT" T6 ON T3."U_EXF_ZONRPTS" = T6."Name"
                INNER JOIN OITW T7 ON T7."ItemCode" = T2."ItemCode" AND T7."WhsCode" = T2."WhsCode"
                INNER JOIN OITM T8 ON T8."ItemCode" = T2."ItemCode"

                WHERE
                T1."DocStatus" = 'O'
                AND T2."Quantity"<>0
                AND T2."WhsCode" = '
                """
                + almacen +
                """
                '
                AND T2."OpenCreQty" != 0
                AND T2."PickStatus" = 'N'
                AND T1."U_FEM_ESTIENDA" = 'N'
                AND T3."U_XM_TerritorioS" != 'Territorio A'
                AND T3."U_EXF_ZONRPTS" != '99'
                AND T3."U_XM_LatitudS" IS NOT NULL 
                AND T3."U_XM_LongitudS" IS NOT NULL 
                AND T3."StreetS"<>'FISCAL'
                AND T1."DocDueDate" <= '
                """
                    + hastavenc +
                    """
                '
                AND T5."SlpCode" IN (
                """
                        + rpt +
                    """
                )
                """
                + strterri +
                """ 
                ORDER BY T1."DocDueDate", T1."DocNum", CASE WHEN T7."OnHand" < (T2."OpenCreQty" * T2."NumPerMsr") THEN 1 ELSE 0 END DESC;
                """;

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
                        item.Almacen = almacen;
                    }

                    ventaConConsolidadoSap = ventaConConsolidadoSap.Where(item => item.Existe != 2).ToList();
                    await connection.CloseAsync();

                    return Ok(ventaConConsolidadoSap);
                }
            } else {

                if (length == 0) {
                    var str = """
                SELECT
                T1."DocNum" AS "NDOCUMENTO",
                T1."DocEntry" AS "ENTRY",
                T2."LineNum" AS "POSICI",
                SUBSTRING(T3."U_XM_LatitudS",0,10) AS "LATITUD",
                SUBSTRING(T3."U_XM_LongitudS",0,10) AS "LONGITUD",
                T3."StreetS" AS "DIRECCION",
                T2."Dscription" AS "NOMBREITEM",
                T2."OpenCreQty" AS "CANTIDAD",
                T2."ItemCode" AS "CODIGOITEM",
                TO_VARCHAR (TO_DATE(T2."ShipDate"), 'DD/MM/YYYY') AS "FECHAMINENTREGA",
                TO_VARCHAR (TO_DATE(T1."DocDueDate"), 'DD/MM/YYYY') AS "FECHAMAXENTREGA",
                '07:30' AS "MINVENTANAHORARIA1",
                '20:30' AS "MAXVENTANAHORARIA1",
                CASE 
                    WHEN COALESCE(T2."Weight1",0) = 0 THEN 0
                    ELSE ROUND((T2."PriceBefDi"/T2."Weight1") ,4)
                END AS "COSTOXKILO",
                T2."GrssProfit" AS "COSTOITEM",
                T2."Weight1" AS "CAPACIDADUNO",
                T4."LicTradNum" AS "IDENTIFICADORCONTACTO",
                T4."CardName" AS "NOMBRECONTACTO",
                T4."Phone1" AS "TELEFONO",
                '' AS "EMAILCONTACTO",
                CASE 
                    WHEN T3."U_XM_TerritorioS" = 'T. MOQUEGUA' THEN 'MQGCNT'
                    WHEN T3."U_XM_TerritorioS" = 'T. CUSCO' THEN 'ALMCUSCO'
                    ELSE 'FEMACO'
                END AS "CTORIGEN",
                T5."SlpName" AS "VENDEDOR",
                T3."U_EXF_ZONRPTS" AS "Name",
                T2."NumPerMsr" AS "Factor",
                T1."Weight" AS "PESOTOTAL",
                T1."GrosProfit" AS "UTILIDADTOTAL",
                T2."GrssProfit" AS "UTILIDAD",
                T7."OnHand" AS "STOCK",
                CASE 
                WHEN T7."OnHand" < (T2."OpenCreQty" * T2."NumPerMsr") THEN 1 ELSE 0
                END AS "ALERTASTOCK",
                T8."InvntryUom" AS "MEDIDABASE"
                FROM ORDR T1
                INNER JOIN RDR1 T2 ON T1."DocEntry"=T2."DocEntry"
                INNER JOIN RDR12 T3 ON T1."DocEntry"=T3."DocEntry"
                INNER JOIN OCRD T4 ON T1."CardCode"=T4."CardCode"
                INNER JOIN OSLP T5 ON T1."SlpCode"=T5."SlpCode"
                LEFT OUTER JOIN "@EXF_ZONRPT" T6 ON T3."U_EXF_ZONRPTS" = T6."Name"
                INNER JOIN OITW T7 ON T7."ItemCode" = T2."ItemCode" AND T7."WhsCode" = T2."WhsCode"
                INNER JOIN OITM T8 ON T8."ItemCode" = T2."ItemCode"

                WHERE
                T1."DocStatus" = 'O'
                AND T2."Quantity"<>0
                AND T2."WhsCode" = '
                """
                + almacen +
                """
                '
                AND T2."OpenCreQty" != 0
                AND T2."PickStatus" = 'N'
                AND T1."U_FEM_ESTIENDA" = 'N'
                AND T3."U_XM_TerritorioS" != 'Territorio A'
                AND T3."U_EXF_ZONRPTS" != '99'
                AND T3."U_XM_LatitudS" IS NOT NULL 
                AND T3."U_XM_LongitudS" IS NOT NULL 
                AND T3."StreetS"<>'FISCAL'
                AND T1."DocDueDate" <= '
                """
                    + hastavenc +
                    """
                '
                AND T3."U_EXF_ZONRPTS" IN (
                """
                    + znrpt +
                    """
                )
                """
                + strterri +
                """ 
                ORDER BY T1."DocDueDate", T1."DocNum", CASE WHEN T7."OnHand" < (T2."OpenCreQty" * T2."NumPerMsr") THEN 1 ELSE 0 END DESC;
                """;

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
                        item.Almacen = almacen;
                    }

                    ventaConConsolidadoSap = ventaConConsolidadoSap.Where(item => item.Existe != 2).ToList();
                    await connection.CloseAsync();

                    return Ok(ventaConConsolidadoSap);
                } else {
                    var str = """
                SELECT
                T1."DocNum" AS "NDOCUMENTO",
                T1."DocEntry" AS "ENTRY",
                T2."LineNum" AS "POSICI",
                SUBSTRING(T3."U_XM_LatitudS",0,10) AS "LATITUD",
                SUBSTRING(T3."U_XM_LongitudS",0,10) AS "LONGITUD",
                T3."StreetS" AS "DIRECCION",
                T2."Dscription" AS "NOMBREITEM",
                T2."OpenCreQty" AS "CANTIDAD",
                T2."ItemCode" AS "CODIGOITEM",
                TO_VARCHAR (TO_DATE(T2."ShipDate"), 'DD/MM/YYYY') AS "FECHAMINENTREGA",
                TO_VARCHAR (TO_DATE(T1."DocDueDate"), 'DD/MM/YYYY') AS "FECHAMAXENTREGA",
                '07:30' AS "MINVENTANAHORARIA1",
                '20:30' AS "MAXVENTANAHORARIA1",
                CASE 
                    WHEN COALESCE(T2."Weight1",0) = 0 THEN 0
                    ELSE ROUND((T2."PriceBefDi"/T2."Weight1") ,4)
                END AS "COSTOXKILO",
                T2."GrssProfit" AS "COSTOITEM",
                T2."Weight1" AS "CAPACIDADUNO",
                T4."LicTradNum" AS "IDENTIFICADORCONTACTO",
                T4."CardName" AS "NOMBRECONTACTO",
                T4."Phone1" AS "TELEFONO",
                '' AS "EMAILCONTACTO",
                CASE 
                    WHEN T3."U_XM_TerritorioS" = 'T. MOQUEGUA' THEN 'MQGCNT'
                    WHEN T3."U_XM_TerritorioS" = 'T. CUSCO' THEN 'ALMCUSCO'
                    ELSE 'FEMACO'
                END AS "CTORIGEN",
                T5."SlpName" AS "VENDEDOR",
                T3."U_EXF_ZONRPTS" AS "Name",
                T2."NumPerMsr" AS "Factor",
                T1."Weight" AS "PESOTOTAL",
                T1."GrosProfit" AS "UTILIDADTOTAL",
                T2."GrssProfit" AS "UTILIDAD",
                T7."OnHand" AS "STOCK",
                CASE 
                WHEN T7."OnHand" < (T2."OpenCreQty" * T2."NumPerMsr") THEN 1 ELSE 0
                END AS "ALERTASTOCK",
                T8."InvntryUom" AS "MEDIDABASE"
                FROM ORDR T1
                INNER JOIN RDR1 T2 ON T1."DocEntry"=T2."DocEntry"
                INNER JOIN RDR12 T3 ON T1."DocEntry"=T3."DocEntry"
                INNER JOIN OCRD T4 ON T1."CardCode"=T4."CardCode"
                INNER JOIN OSLP T5 ON T1."SlpCode"=T5."SlpCode"
                LEFT OUTER JOIN "@EXF_ZONRPT" T6 ON T3."U_EXF_ZONRPTS" = T6."Name"
                INNER JOIN OITW T7 ON T7."ItemCode" = T2."ItemCode" AND T7."WhsCode" = T2."WhsCode"
                INNER JOIN OITM T8 ON T8."ItemCode" = T2."ItemCode"

                WHERE
                T1."DocStatus" = 'O' 
                AND T2."Quantity"<>0
                AND T2."WhsCode" = '
                """
                + almacen +
                """
                '
                AND T2."OpenCreQty" != 0
                AND T2."PickStatus" = 'N'
                AND T1."U_FEM_ESTIENDA" = 'N'
                AND T3."U_XM_TerritorioS" != 'Territorio A'
                AND T3."U_EXF_ZONRPTS" != '99'
                AND T3."U_XM_LatitudS" IS NOT NULL 
                AND T3."U_XM_LongitudS" IS NOT NULL 
                AND T3."StreetS"<>'FISCAL'
                AND T1."DocDate" >=  '
                AND T1."DocDueDate" <= '
                """
                    + hastavenc +
                    """
                '
                AND T3."U_EXF_ZONRPTS" IN (
                """
                    + znrpt +
                    """
                )
                AND T5."SlpCode" IN (
                """
                        + rpt +
                    """
                )
                """
                + strterri +
                """ 
                ORDER BY T1."DocDueDate", T1."DocNum", CASE WHEN T7."OnHand" < (T2."OpenCreQty" * T2."NumPerMsr") THEN 1 ELSE 0 END DESC;
                """;

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
                        item.Almacen = almacen;
                    }

                    ventaConConsolidadoSap = ventaConConsolidadoSap.Where(item => item.Existe != 2).ToList();
                    await connection.CloseAsync();

                    return Ok(ventaConConsolidadoSap);
                }
            }
        }
        public async Task<IActionResult> VerRegistrado() {
            string connectionString = "Server=192.168.1.15;Database=femacoco_Seguimientos;Uid=femacoco_seguimiento;Pwd=F3m4c05rl;";
            using MySqlConnection mysqlConnection = new MySqlConnection(connectionString);

            await mysqlConnection.OpenAsync();
            string sqlQuery = @"SELECT * FROM CodigosBeetrack";
            IEnumerable<VerRegistrado> ventaSinConsolidado = await mysqlConnection.QueryAsync<VerRegistrado>(sqlQuery);
            return Ok(ventaSinConsolidado);
        }

        public async Task<IActionResult> VerResumido(string desde, string hasta, string desdevenc, string hastavenc, string rpt, string terri, string znrpt) {
            string text = "" + rpt + "";
            string textzn = "" + znrpt + "";
            int length = text.Length;
            int lengthzn = textzn.Length;
            HanaConnection hanaConnection = new(_hanaConnectionString);
            hanaConnection.Open();
            if (lengthzn == 0) {
                if (length == 0) {
                    var str = """
                SELECT
                ROW_NUMBER() OVER(PARTITION BY T1."CardCode" ORDER BY T1."CardCode") AS "NUMMANI",
                T1."DocNum" as "NDOCUMENTO",
                T5."GroupName" AS "NOMBREGRUPO",
                T1."DocEntry" as "ENTRY",
                T2."StreetS" as "DIRECCION",
                T1."CardCode" as "IDENTIFICADORCONTACTO",
                T1."CardName" as "NOMBRECONTACTO",
                T4."Phone1" as "TELEFONO",
                T3."SlpName" as "VENDEDOR",
                T2."U_EXF_ZONRPTS" as "Name",
                T2."U_XM_TerritorioS" as "TERRI",
                T1."GrosProfit" AS "UTILIDAD",
                (SELECT SUM("Weight1") FROM RDR1 T8 WHERE T8."DocEntry" = T1."DocEntry"  ) AS "PESOTOTAL"

                FROM ORDR T1
                INNER JOIN RDR12 T2 ON T1."DocEntry"=T2."DocEntry"
                INNER JOIN OSLP T3 ON T3."SlpCode" = T1."SlpCode"
                inner join OCRD T4 on T1."CardCode" = T4."CardCode"
                LEFT JOIN OCRG T5 ON T5."GroupCode" = T4."GroupCode"
                WHERE
                T1."DocStatus" = 'O'
                AND T1."U_FEM_ESTIENDA" = 'N'
                AND T2."U_XM_TerritorioS" != 'Territorio A'
                AND T2."U_EXF_ZONRPTS" != '99'
                AND (T2."U_XM_LatitudS" IS NOT NULL AND TO_DOUBLE(T2."U_XM_LatitudS") != 0 AND TO_DOUBLE(T2."U_XM_LatitudS") BETWEEN -90 AND 90)
                AND (T2."U_XM_LongitudS" IS NOT NULL AND TO_DOUBLE(T2."U_XM_LongitudS") != 0 AND TO_DOUBLE(T2."U_XM_LongitudS") BETWEEN -180 AND 180)
                AND T2."StreetS"<>'FISCAL'
                AND T1."DocDueDate" <= '
                """
                    + hastavenc +
                    """
                ' 
                AND T2."U_XM_TerritorioS" IN (
                """
                    + terri +
                    """
                )    
                ORDER BY "NUMMANI"
                """;

                    Console.WriteLine(str);
                    IEnumerable<VerResumido> verResumidoSap = hanaConnection.Query<VerResumido>(str);
                    hanaConnection.Close();
                    var verResumido = verResumidoSap;
                    Console.WriteLine(str);
                    return Ok(verResumido);
                } else {
                    var str = """
                SELECT
                ROW_NUMBER() OVER(PARTITION BY T1."CardCode" ORDER BY T1."CardCode") AS "NUMMANI",
                T1."DocNum" as "NDOCUMENTO",
                T5."GroupName" AS "NOMBREGRUPO",
                T1."DocEntry" as "ENTRY",
                T2."StreetS" as "DIRECCION",
                T1."CardCode" as "IDENTIFICADORCONTACTO",
                T1."CardName" as "NOMBRECONTACTO",
                T4."Phone1" as "TELEFONO",
                T3."SlpName" as "VENDEDOR",
                T2."U_EXF_ZONRPTS" as "Name",
                T2."U_XM_TerritorioS" as "TERRI",
                T1."GrosProfit" AS "UTILIDAD",
                (SELECT SUM("Weight1") FROM RDR1 T8 WHERE T8."DocEntry" = T1."DocEntry"  ) AS "PESOTOTAL"

                FROM ORDR T1
                INNER JOIN RDR12 T2 ON T1."DocEntry"=T2."DocEntry"
                INNER JOIN OSLP T3 ON T3."SlpCode" = T1."SlpCode"
                inner join OCRD T4 on T1."CardCode" = T4."CardCode"
                LEFT JOIN OCRG T5 ON T5."GroupCode" = T4."GroupCode"
                WHERE
                T1."DocStatus" = 'O'
                AND T1."U_FEM_ESTIENDA" = 'N'
                AND T2."U_XM_TerritorioS" != 'Territorio A'
                AND T2."U_EXF_ZONRPTS" != '99'
                AND (T2."U_XM_LatitudS" IS NOT NULL AND TO_DOUBLE(T2."U_XM_LatitudS") != 0 AND TO_DOUBLE(T2."U_XM_LatitudS") BETWEEN -90 AND 90)
                AND (T2."U_XM_LongitudS" IS NOT NULL AND TO_DOUBLE(T2."U_XM_LongitudS") != 0 AND TO_DOUBLE(T2."U_XM_LongitudS") BETWEEN -180 AND 180)
                AND T2."StreetS"<>'FISCAL'
                AND T1."DocDueDate" <= '
                """
                    + hastavenc +
                    """
                '
                AND T1."SlpCode" IN (
                """
                        + rpt +
                    """
                )
                AND T2."U_XM_TerritorioS" IN (
                """
                    + terri +
                    """
                )
                ORDER BY "NUMMANI"
                """;

                    Console.WriteLine(str);
                    IEnumerable<VerResumido> verResumidoSap = hanaConnection.Query<VerResumido>(str);
                    hanaConnection.Close();
                    var verResumido = verResumidoSap;

                    Console.WriteLine(str);
                    return Ok(verResumido);
                }
            } else {
                if (length == 0) {
                    var str = """
                SELECT
                ROW_NUMBER() OVER(PARTITION BY T1."CardCode" ORDER BY T1."CardCode") AS "NUMMANI",
                T1."DocNum" as "NDOCUMENTO",
                T5."GroupName" AS "NOMBREGRUPO",
                T1."DocEntry" as "ENTRY",
                T2."StreetS" as "DIRECCION",
                T1."CardCode" as "IDENTIFICADORCONTACTO",
                T1."CardName" as "NOMBRECONTACTO",
                T4."Phone1" as "TELEFONO",
                T3."SlpName" as "VENDEDOR",
                T2."U_EXF_ZONRPTS" as "Name",
                T2."U_XM_TerritorioS" as "TERRI",
                T1."GrosProfit" AS "UTILIDAD",
                (SELECT SUM("Weight1") FROM RDR1 T8 WHERE T8."DocEntry" = T1."DocEntry"  ) AS "PESOTOTAL"

                FROM ORDR T1
                INNER JOIN RDR12 T2 ON T1."DocEntry"=T2."DocEntry"
                INNER JOIN OSLP T3 ON T3."SlpCode" = T1."SlpCode"
                inner join OCRD T4 on T1."CardCode" = T4."CardCode"
                LEFT JOIN OCRG T5 ON T5."GroupCode" = T4."GroupCode"
                WHERE
                T1."DocStatus" = 'O'
                AND T1."U_FEM_ESTIENDA" = 'N'
                AND T2."U_XM_TerritorioS" != 'Territorio A'
                AND T2."U_EXF_ZONRPTS" != '99'
                AND (T2."U_XM_LatitudS" IS NOT NULL AND TO_DOUBLE(T2."U_XM_LatitudS") != 0 AND TO_DOUBLE(T2."U_XM_LatitudS") BETWEEN -90 AND 90)
                AND (T2."U_XM_LongitudS" IS NOT NULL AND TO_DOUBLE(T2."U_XM_LongitudS") != 0 AND TO_DOUBLE(T2."U_XM_LongitudS") BETWEEN -180 AND 180)
                AND T2."StreetS"<>'FISCAL'
                AND T1."DocDueDate" <= '
                """
                    + hastavenc +
                    """
                ' 
                AND T2."U_EXF_ZONRPTS" IN (
                """
                + znrpt +
                """
                )
                AND T2."U_XM_TerritorioS" IN (
                """
                    + terri +
                    """
                ) 
                ORDER BY "NUMMANI"
                """;

                    Console.WriteLine(str);
                    IEnumerable<VerResumido> verResumidoSap = hanaConnection.Query<VerResumido>(str);
                    hanaConnection.Close();
                    var verResumido = verResumidoSap;
                    Console.WriteLine(str);
                    return Ok(verResumido);
                } else {
                    var str = """
                SELECT
                ROW_NUMBER() OVER(PARTITION BY T1."CardCode" ORDER BY T1."CardCode") AS "NUMMANI",
                T1."DocNum" as "NDOCUMENTO",
                T5."GroupName" AS "NOMBREGRUPO",
                T1."DocEntry" as "ENTRY",
                T2."StreetS" as "DIRECCION",
                T1."CardCode" as "IDENTIFICADORCONTACTO",
                T1."CardName" as "NOMBRECONTACTO",
                T4."Phone1" as "TELEFONO",
                T3."SlpName" as "VENDEDOR",
                T2."U_EXF_ZONRPTS" as "Name",
                T2."U_XM_TerritorioS" as "TERRI",
                T1."GrosProfit" AS "UTILIDAD",
                (SELECT SUM("Weight1") FROM RDR1 T8 WHERE T8."DocEntry" = T1."DocEntry"  ) AS "PESOTOTAL"

                FROM ORDR T1
                INNER JOIN RDR12 T2 ON T1."DocEntry"=T2."DocEntry"
                INNER JOIN OSLP T3 ON T3."SlpCode" = T1."SlpCode"
                inner join OCRD T4 on T1."CardCode" = T4."CardCode"
                LEFT JOIN OCRG T5 ON T5."GroupCode" = T4."GroupCode"
                WHERE
                T1."DocStatus" = 'O'
                AND T1."U_FEM_ESTIENDA" = 'N'
                AND T2."U_XM_TerritorioS" != 'Territorio A'
                AND T2."U_EXF_ZONRPTS" != '99'
                AND (T2."U_XM_LatitudS" IS NOT NULL AND TO_DOUBLE(T2."U_XM_LatitudS") != 0 AND TO_DOUBLE(T2."U_XM_LatitudS") BETWEEN -90 AND 90)
                AND (T2."U_XM_LongitudS" IS NOT NULL AND TO_DOUBLE(T2."U_XM_LongitudS") != 0 AND TO_DOUBLE(T2."U_XM_LongitudS") BETWEEN -180 AND 180)
                AND T2."StreetS"<>'FISCAL'
                AND T1."DocDueDate" <= '
                """
                    + hastavenc +
                    """
                '
                AND T2."U_EXF_ZONRPTS" IN (
                """
                + znrpt +
                """
                )
                AND T1."SlpCode" IN (
                """
                        + rpt +
                    """
                )
                AND T2."U_XM_TerritorioS" IN (
                """
                    + terri +
                    """
                )
                ORDER BY "NUMMANI"
                """;
                    Console.WriteLine(str);
                    IEnumerable<VerResumido> verResumidoSap = hanaConnection.Query<VerResumido>(str);
                    hanaConnection.Close();
                    var verResumido = verResumidoSap;

                    Console.WriteLine(str);
                    return Ok(verResumido);
                }
            }

        }

        public async Task<IActionResult> VerVendedor(string desde, string hasta, string desdevenc, string hastavenc, string rpt, string znrpt) {
            string text = "" + rpt + "";
            string textzn = "" + znrpt + "";
            int length = text.Length;
            int lengthzn = textzn.Length;
            Console.WriteLine("The length of the string is: " + length + rpt);
            HanaConnection hanaConnection = new(_hanaConnectionString);
            hanaConnection.Open();
            if (lengthzn == 0) {
                if (length == 0) {
                    var str = """
                SELECT
                T1."DocNum" as "NDOCUMENTO",
                T8."GroupName" AS "NOMBREGRUPO",
                T1."DocEntry" as "ENTRY",
                T1."Address2" as "DIRECCION",
                T4."LicTradNum" as "IDENTIFICADORCONTACTO",
                T4."CardName" as "NOMBRECONTACTO",
                T4."Phone1" as "TELEFONO",
                T5."SlpName" as "VENDEDOR",
                T3."U_EXF_ZONRPTS" as "Name",
                T3."U_XM_TerritorioS" as "TERRI",
                T2."Quantity" AS "OpenQty",
                T3."StreetS" AS "Address",
                T1."DocDate",
                T1."DocDueDate" as "ShipDate",
                T2."Dscription" as "NOMBREITEM",
                T2."Quantity" as "CANTIDAD",
                T2."ItemCode" as "CODIGOITEM",
                T2."PriceAfVAT" as "COSTOITEM",
                T2."Weight1" AS "PESO",
                T1."GrosProfit" AS "UTILIDAD",
                T3."U_XM_LatitudS" as "LATITUD",
                T3."U_XM_LongitudS" as "LONGITUD",
                CASE
                    WHEN T3."U_XM_TerritorioS" = 'Territorio A' OR T3."U_EXF_ZONRPTS" = '99' OR T3."U_XM_LatitudS" IS NULL OR T3."U_XM_LongitudS" IS NULL OR T1."Address2" = 'FISCAL' OR T3."U_XM_TerritorioS" IS NULL OR TO_DOUBLE(T3."U_XM_LatitudS") = 0 OR TO_DOUBLE(T3."U_XM_LongitudS") = 0 OR TO_DOUBLE(T3."U_XM_LatitudS") < -90 OR TO_DOUBLE(T3."U_XM_LatitudS") > 90 OR TO_DOUBLE(T3."U_XM_LongitudS") < -180 OR TO_DOUBLE(T3."U_XM_LongitudS") > 180 OR TO_DOUBLE(T2."Weight1") = 0 OR T2."Weight1" IS NULL THEN 1
                ELSE 0
                END AS "ALERTA"
                FROM ORDR T1
                INNER JOIN RDR1 T2 ON T1."DocEntry"=T2."DocEntry"
                INNER JOIN RDR12 T3 ON T1."DocEntry"=T3."DocEntry"
                INNER JOIN OCRD T4 ON T1."CardCode"=T4."CardCode"
                INNER JOIN OSLP T5 ON T1."SlpCode"=T5."SlpCode"
                LEFT JOIN OCRG T8 ON T8."GroupCode" = T4."GroupCode"
                WHERE
                T1."DocStatus" = 'O'
                AND T1."U_FEM_ESTIENDA" = 'N'
                AND T1."DocDueDate" <= '
                """
                    + hastavenc +
                    """
                '
                ORDER BY T1."DocNum", T2."Weight1"
                """;
                    IEnumerable<VerVendedor> verVendedorSap = hanaConnection.Query<VerVendedor>(str);
                    hanaConnection.Close();
                    var verVendedor = verVendedorSap;
                    Console.WriteLine(str);
                    return Ok(verVendedor);
                } else {
                    var str = """
                SELECT
                T1."DocNum" as "NDOCUMENTO",
                T8."GroupName" AS "NOMBREGRUPO",
                T1."DocEntry" as "ENTRY",
                T1."Address2" as "DIRECCION",
                T4."LicTradNum" as "IDENTIFICADORCONTACTO",
                T4."CardName" as "NOMBRECONTACTO",
                T4."Phone1" as "TELEFONO",
                T5."SlpName" as "VENDEDOR",
                T3."U_EXF_ZONRPTS" as "Name",
                T3."U_XM_TerritorioS" as "TERRI",
                T2."Quantity" AS "OpenQty",
                T3."StreetS" AS "Address",
                T1."DocDate",
                T1."DocDueDate" as "ShipDate",
                T2."Dscription" as "NOMBREITEM",
                T2."Quantity" as "CANTIDAD",
                T2."ItemCode" as "CODIGOITEM",
                T2."PriceAfVAT" as "COSTOITEM",
                T2."Weight1" AS "PESO",
                T1."GrosProfit" AS "UTILIDAD",
                T3."U_XM_LatitudS" as "LATITUD",
                T3."U_XM_LongitudS" as "LONGITUD",
                CASE
                    WHEN T3."U_XM_TerritorioS" = 'Territorio A' OR T3."U_EXF_ZONRPTS" = '99' OR T3."U_XM_LatitudS" IS NULL OR T3."U_XM_LongitudS" IS NULL OR T1."Address2" = 'FISCAL' OR T3."U_XM_TerritorioS" IS NULL OR TO_DOUBLE(T3."U_XM_LatitudS") = 0 OR TO_DOUBLE(T3."U_XM_LongitudS") = 0 OR TO_DOUBLE(T3."U_XM_LatitudS") < -90 OR TO_DOUBLE(T3."U_XM_LatitudS") > 90 OR TO_DOUBLE(T3."U_XM_LongitudS") < -180 OR TO_DOUBLE(T3."U_XM_LongitudS") > 180 OR TO_DOUBLE(T2."Weight1") = 0 OR T2."Weight1" IS NULL THEN 1
                ELSE 0
                END AS "ALERTA"
                FROM ORDR T1
                INNER JOIN RDR1 T2 ON T1."DocEntry"=T2."DocEntry"
                INNER JOIN RDR12 T3 ON T1."DocEntry"=T3."DocEntry"
                INNER JOIN OCRD T4 ON T1."CardCode"=T4."CardCode"
                INNER JOIN OSLP T5 ON T1."SlpCode"=T5."SlpCode"
                LEFT JOIN OCRG T8 ON T8."GroupCode" = T4."GroupCode"
                WHERE
                T1."DocStatus" = 'O'
                AND T1."U_FEM_ESTIENDA" = 'N'
                AND T1."DocDueDate" <= '
                """
                    + hastavenc +
                    """
                '
                AND T5."SlpCode" IN (
                """
                        + rpt +
                    """
                )
                ORDER BY T1."DocNum", T2."Weight1"
                """;
                    IEnumerable<VerVendedor> verVendedorSap = hanaConnection.Query<VerVendedor>(str);
                    hanaConnection.Close();
                    var verVendedor = verVendedorSap;

                    Console.WriteLine(str);
                    return Ok(verVendedor);
                }
            } else {
                if (length == 0) {
                    var str = """
                SELECT
                T1."DocNum" as "NDOCUMENTO",
                T8."GroupName" AS "NOMBREGRUPO",
                T1."DocEntry" as "ENTRY",
                T1."Address2" as "DIRECCION",
                T4."LicTradNum" as "IDENTIFICADORCONTACTO",
                T4."CardName" as "NOMBRECONTACTO",
                T4."Phone1" as "TELEFONO",
                T5."SlpName" as "VENDEDOR",
                T3."U_EXF_ZONRPTS" as "Name",
                T3."U_XM_TerritorioS" as "TERRI",
                T2."Quantity" AS "OpenQty",
                T3."StreetS" AS "Address",
                T1."DocDate",
                T1."DocDueDate" as "ShipDate",
                T2."Dscription" as "NOMBREITEM",
                T2."Quantity" as "CANTIDAD",
                T2."ItemCode" as "CODIGOITEM",
                T2."PriceAfVAT" as "COSTOITEM",
                T2."Weight1" AS "PESO",
                T1."GrosProfit" AS "UTILIDAD",
                T3."U_XM_LatitudS" as "LATITUD",
                T3."U_XM_LongitudS" as "LONGITUD",
                CASE
                    WHEN T3."U_XM_TerritorioS" = 'Territorio A' OR T3."U_EXF_ZONRPTS" = '99' OR T3."U_XM_LatitudS" IS NULL OR T3."U_XM_LongitudS" IS NULL OR T1."Address2" = 'FISCAL' OR T3."U_XM_TerritorioS" IS NULL OR TO_DOUBLE(T3."U_XM_LatitudS") = 0 OR TO_DOUBLE(T3."U_XM_LongitudS") = 0 OR TO_DOUBLE(T3."U_XM_LatitudS") < -90 OR TO_DOUBLE(T3."U_XM_LatitudS") > 90 OR TO_DOUBLE(T3."U_XM_LongitudS") < -180 OR TO_DOUBLE(T3."U_XM_LongitudS") > 180 OR TO_DOUBLE(T2."Weight1") = 0 OR T2."Weight1" IS NULL THEN 1
                ELSE 0
                END AS "ALERTA"
                FROM ORDR T1
                INNER JOIN RDR1 T2 ON T1."DocEntry"=T2."DocEntry"
                INNER JOIN RDR12 T3 ON T1."DocEntry"=T3."DocEntry"
                INNER JOIN OCRD T4 ON T1."CardCode"=T4."CardCode"
                INNER JOIN OSLP T5 ON T1."SlpCode"=T5."SlpCode"
                LEFT JOIN OCRG T8 ON T8."GroupCode" = T4."GroupCode"
                WHERE
                T1."DocStatus" = 'O'
                AND T1."U_FEM_ESTIENDA" = 'N'
                AND T1."DocDueDate" <= '
                """
                    + hastavenc +
                    """
                '
                AND T3."U_EXF_ZONRPTS" IN (
                """
                + znrpt +
                """
                )
                ORDER BY T1."DocNum", T2."Weight1"
                """;
                    IEnumerable<VerVendedor> verVendedorSap = hanaConnection.Query<VerVendedor>(str);
                    hanaConnection.Close();
                    var verVendedor = verVendedorSap;
                    Console.WriteLine(str);
                    return Ok(verVendedor);
                } else {
                    var str = """
                SELECT
                T1."DocNum" as "NDOCUMENTO",
                T8."GroupName" AS "NOMBREGRUPO",
                T1."DocEntry" as "ENTRY",
                T1."Address2" as "DIRECCION",
                T4."LicTradNum" as "IDENTIFICADORCONTACTO",
                T4."CardName" as "NOMBRECONTACTO",
                T4."Phone1" as "TELEFONO",
                T5."SlpName" as "VENDEDOR",
                T3."U_EXF_ZONRPTS" as "Name",
                T3."U_XM_TerritorioS" as "TERRI",
                T2."Quantity" as "OpenQty",
                T3."StreetS" AS "Address",
                T1."DocDate",
                T1."DocDueDate" as "ShipDate",
                T2."Dscription" as "NOMBREITEM",
                T2."Quantity" as "CANTIDAD",
                T2."ItemCode" as "CODIGOITEM",
                T2."PriceAfVAT" as "COSTOITEM",
                T2."Weight1" AS "PESO",
                T1."GrosProfit" AS "UTILIDAD",
                T3."U_XM_LatitudS" as "LATITUD",
                T3."U_XM_LongitudS" as "LONGITUD",
                CASE
                    WHEN T3."U_XM_TerritorioS" = 'Territorio A' OR T3."U_EXF_ZONRPTS" = '99' OR T3."U_XM_LatitudS" IS NULL OR T3."U_XM_LongitudS" IS NULL OR T1."Address2" = 'FISCAL' OR T3."U_XM_TerritorioS" IS NULL OR TO_DOUBLE(T3."U_XM_LatitudS") = 0 OR TO_DOUBLE(T3."U_XM_LongitudS") = 0 OR TO_DOUBLE(T3."U_XM_LatitudS") < -90 OR TO_DOUBLE(T3."U_XM_LatitudS") > 90 OR TO_DOUBLE(T3."U_XM_LongitudS") < -180 OR TO_DOUBLE(T3."U_XM_LongitudS") > 180 OR TO_DOUBLE(T2."Weight1") = 0 OR T2."Weight1" IS NULL THEN 1
                ELSE 0
                END AS "ALERTA"
                FROM ORDR T1
                INNER JOIN RDR1 T2 ON T1."DocEntry"=T2."DocEntry"
                INNER JOIN RDR12 T3 ON T1."DocEntry"=T3."DocEntry"
                INNER JOIN OCRD T4 ON T1."CardCode"=T4."CardCode"
                INNER JOIN OSLP T5 ON T1."SlpCode"=T5."SlpCode"
                LEFT JOIN OCRG T8 ON T8."GroupCode" = T4."GroupCode"
                WHERE
                T1."DocStatus" = 'O'
                AND T1."U_FEM_ESTIENDA" = 'N'
                AND T1."DocDueDate" <= '
                """
                    + hastavenc +
                    """
                '
                AND T3."U_EXF_ZONRPTS" IN (
                """
                + znrpt +
                """
                )
                AND T5."SlpCode" IN (
                """
                        + rpt +
                    """
                )
                ORDER BY T1."DocNum", T2."Weight1"
                """;
                    IEnumerable<VerVendedor> verVendedorSap = hanaConnection.Query<VerVendedor>(str);
                    hanaConnection.Close();
                    var verVendedor = verVendedorSap;

                    Console.WriteLine(str);
                    return Ok(verVendedor);
                }
            }

        }

        [HttpPost]
        public async Task<IActionResult> validarLista([FromBody] string numerosGuia) {

            dynamic jsonObject = JsonConvert.DeserializeObject(numerosGuia, typeof(ExpandoObject));
            var pickedValues = new List<int>();
            HanaConnection hanaConnection = new(_hanaConnectionString);
            try {
                hanaConnection.Open();
                foreach (dynamic i in jsonObject.numerosGuia) {
                    var query = $"""SELECT MAX(COALESCE(T1."PickIdNo",0)) AS "PICKED" FROM ORDR T0 INNER JOIN RDR1 T1 ON T1."DocEntry" = T0."DocEntry" WHERE T0."DocNum" = '{i}'""";
                    var res = hanaConnection.Query<object>(query).FirstOrDefault();
                    var data = (IDictionary<string, object>)res;
                    var pickedValue = Convert.ToInt32(data["PICKED"]);
                    pickedValues.Add(pickedValue);
                    Console.WriteLine(data["PICKED"]);
                }
                Console.WriteLine(pickedValues.Select(item => item));
            } catch (Exception ex) {
                Console.WriteLine($"Error: {ex.Message}");
            }
            return Json(pickedValues);
        }

        public async Task<IActionResult> NumeroBeetrack([FromBody] string numerosGuia) {

            ObtenerUsuarioYClave();

            dynamic jsonObject = JsonConvert.DeserializeObject(numerosGuia, typeof(ExpandoObject));
            var errorList = new List<object>();

            string sessionId = null;
            HttpClientHandler clientHandlerl = new HttpClientHandler();
            clientHandlerl.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => { return true; };
            using (var httpClient = new HttpClient(clientHandlerl)) {
                var json = JsonConvert.SerializeObject(new {
                    CompanyDB = "FEMACO_PROD",  
                    Password = clave,
                    UserName = usuario
                });
                var httpContentl = new StringContent(json);
                httpContentl.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                var responsel = await httpClient.PostAsync("https://192.168.1.9:50000/b1s/v1/Login", httpContentl);

                if (responsel.IsSuccessStatusCode) {
                    var responseContentl = await responsel.Content.ReadAsStringAsync();
                    dynamic responseObject = JsonConvert.DeserializeObject(responseContentl);
                    sessionId = responseObject.SessionId;
                    Console.WriteLine(responseContentl);
                } else {
                    Console.WriteLine($"Error en la solicitud: {responsel.StatusCode}");
                }
            }

            try {
                var f = 1;
                foreach (dynamic i in jsonObject) {

                    HttpClientHandler clientHandler = new HttpClientHandler();
                    clientHandler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => { return true; };
                    using (var httpClient = new HttpClient(clientHandler)) {
                        var json = JsonConvert.SerializeObject(new { U_FEM_NUMBTRK = i.vehiculo + i.mani });
                        var httpContent = new StringContent(json);
                        httpContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                        httpClient.DefaultRequestHeaders.Add("Cookie", "B1SESSION=" + sessionId);

                        var response = await httpClient.PatchAsync("https://192.168.1.9:50000/b1s/v1/Orders(" + i.idsap + ")", httpContent);

                        if (response.IsSuccessStatusCode) {
                            var responseContent = await response.Content.ReadAsStringAsync();
                            Console.WriteLine(responseContent + "Bien" + f);
                            f++;
                        } else {
                            var responseContent = await response.Content.ReadAsStringAsync();
                            dynamic jsonResponse = Newtonsoft.Json.JsonConvert.DeserializeObject(responseContent);
                            string errorMessage = jsonResponse.error.message.value;
                            Console.WriteLine($"Error en la solicitud:" + i.numeroguia + " " + errorMessage);
                            errorList.Add(new { numeroguia = i.numeroguia, errorMessage });
                        }
                    }
                }
                Console.WriteLine("Terminado");
            } catch (Exception ex) {
                Console.WriteLine($"Error mi king: {ex.Message}");
            }
            return Json(errorList);
        }

        public async Task<IActionResult> VerConManifiesto() {
            HanaConnection hanaConnection = new(_hanaConnectionString);
            hanaConnection.Open();

            var str = """
                SELECT 
                "@EXP_MANC"."DocEntry" AS "NumManifiesto",
                'ABIERTO' AS "EstadoManifiesto",
                "@EXP_MANC"."U_EXP_PLVE" AS "Placa",
                "@EXP_MANC"."U_EXP_COND" AS "Conductor",
                "OINV"."DocEntry" AS "Codigo Factura",
                "OINV"."DocNum" AS "NumeroFactura",
                "ORRR"."DocNum" AS "NumeroSolicitud",
                "ORRR"."DocDate" AS "Fecha",
                "ORRR"."DocTotal" AS "Total",
                'ABIERTO' AS "EstadoSolicitud"
                 FROM "@EXP_MANC" 
                INNER JOIN "@EXP_MAND" ON "@EXP_MAND"."DocEntry" = "@EXP_MANC"."DocEntry" 
                INNER JOIN "OINV" ON "OINV"."DocEntry" = "@EXP_MAND"."U_EXP_FADOCENTRY" 
                LEFT JOIN "ORRR" ON "ORRR"."U_LB_NumeroFactura" = "OINV"."DocNum"
                WHERE "@EXP_MANC"."U_EXP_ESTA" = 'O' AND "ORRR"."DocStatus" = 'O'
                ORDER BY "OINV"."DocEntry"
                """;
            IEnumerable<VerConManifiesto> ConManifiestoSap = hanaConnection.Query<VerConManifiesto>(str);
            hanaConnection.Close();
            var ConManifiesto = ConManifiestoSap;
            Console.WriteLine(str);
            return Ok(ConManifiesto);
        }

        [HttpPost]
        public async Task<IActionResult> MostrarDatos([FromForm] IFormFile ArchivoExcel, [FromForm] string codigo) {

            List<string> codigosBD;
            using (var connection = new SqlConnection(_connectionString)) {
                string query = @"
                SELECT T0.Codigo 
                FROM PedidosBeetrack T0 
                WHERE T0.General = @codigo";

                var result = await connection.QueryAsync<string>(query, new { codigo });
                codigosBD = result.ToList();
            }

            Stream stream = ArchivoExcel.OpenReadStream();
            IWorkbook MiExcel = null;
            if (Path.GetExtension(ArchivoExcel.FileName) == ".xlsx") {
                MiExcel = new XSSFWorkbook(stream);
            } else {
                MiExcel = new HSSFWorkbook(stream);
            }

            List<VMContacto> listaTotal = new List<VMContacto>();
            List<string> codigosConCoincidencia = new List<string>();
            List<string> codigosSinCoincidencia = new List<string>(codigosBD);
            for (int hojaIndex = 0; hojaIndex < MiExcel.NumberOfSheets; hojaIndex++) {
                ISheet hoja = MiExcel.GetSheetAt(hojaIndex);

                IRow filaPlaca = hoja.GetRow(20);
                ICell celdaPlaca = filaPlaca.GetCell(1);
                string vehiculo = celdaPlaca?.ToString();

                IRow filaCapacidad = hoja.GetRow(13);
                ICell celdaCapacidad = filaCapacidad.GetCell(1);
                string capacidad = celdaCapacidad?.ToString();

                IRow filaUtilizado = hoja.GetRow(15);
                ICell celdaUtilizado = filaUtilizado.GetCell(1);
                string utilizado = celdaUtilizado?.ToString();

                int cantidadFilas = hoja.LastRowNum;

                List<VMContacto> lista = new List<VMContacto>();

                for (int i = 29; i <= cantidadFilas; i++) {
                    IRow fila = hoja.GetRow(i);

                    string numeroguia = fila.GetCell(0)?.ToString();

                    if (codigosBD.Contains(numeroguia)) {
                        if (codigosSinCoincidencia.Contains(numeroguia)) {
                            codigosSinCoincidencia.Remove(numeroguia);
                            codigosConCoincidencia.Add(numeroguia);
                        }
                    }


                    lista.Add(new VMContacto {
                        vehiculo = vehiculo,
                        capacidad = capacidad,
                        utilizado = utilizado,
                        numeroguia = numeroguia,
                        item = fila.GetCell(1)?.ToString(),
                        cantidad = fila.GetCell(2)?.ToString(),
                        peso = fila.GetCell(3)?.ToString(),
                        cliente = fila.GetCell(5)?.ToString(),
                        direccion = fila.GetCell(6)?.ToString(),
                        idsap = fila.GetCell(7)?.ToString(),
                        vendedor = fila.GetCell(8)?.ToString(),
                    });
                }

                listaTotal.AddRange(lista);
            }

            return Ok(new {
                listaTotal,
                codigosConCoincidencia,
                codigosSinCoincidencia
            });
        }


        [HttpPost]
        public async Task<IActionResult> GenerarPlanificacionProvincia([FromBody] Dictionary<string, PlacaProvincia> datos) {

            try {
                Console.WriteLine(datos);
                if (datos == null || !datos.Any()) {
                    return BadRequest(new { message = "No se han recibido datos válidos." });
                }
                ObtenerUsuarioYClave();

                HanaConnection hanaConnection = new(_hanaConnectionString);
                string codigo = DateTime.Now.ToString("yyMMddHHmmss");
                string almacen = null;
                foreach (var placa in datos) {
                    using (var connection = new SqlConnection(_connectionString)) {

                        await hanaConnection.OpenAsync();
                        await connection.OpenAsync();

                        almacen = placa.Value.Pedidos[0].almacen;

                        string queryPlan = "INSERT INTO Planificacion (Fecha, Almacen, Usuario, Clave) OUTPUT INSERTED.IDPlan VALUES (GETDATE(), @Almacen, @Usuario, @Clave)";
                        using (SqlCommand cmdPlan = new SqlCommand(queryPlan, connection)) {
                            cmdPlan.Parameters.AddWithValue("@Almacen", almacen);
                            cmdPlan.Parameters.AddWithValue("@Usuario", usuario);
                            cmdPlan.Parameters.AddWithValue("@Clave", clave);

                            int idPlan = (int)await cmdPlan.ExecuteScalarAsync();

                            string queryCodigo = "INSERT INTO CodigosBeetrack (Codigo, Fecha, Almacen, IDPlan, ExcelSubido, Tipo) VALUES (@Codigo, GETDATE(), @Almacen, @IDPlan, 1, 'Provincia')";
                            using (SqlCommand cmdCodigo = new SqlCommand(queryCodigo, connection)) {
                                cmdCodigo.Parameters.AddWithValue("@Codigo", codigo);
                                cmdCodigo.Parameters.AddWithValue("@Almacen", almacen);
                                cmdCodigo.Parameters.AddWithValue("@IDPlan", idPlan);
                                await cmdCodigo.ExecuteNonQueryAsync();
                            }

                            string queryPlaca = "INSERT INTO PlanificacionPlaca(IDPlan, Placa, Capacidad) OUTPUT INSERTED.IDPlanPla VALUES(@IDPlan, @Placa, @Capacidad)";
                            using (SqlCommand cmdPlaca = new SqlCommand(queryPlaca, connection)) {
                                cmdPlaca.Parameters.AddWithValue("@IDPlan", idPlan);
                                cmdPlaca.Parameters.AddWithValue("@Placa", placa.Key);
                                cmdPlaca.Parameters.AddWithValue("@Capacidad", placa.Value.Capacidad);

                                int idPlanPla = (int)await cmdPlaca.ExecuteScalarAsync();

                                string queryMani = "INSERT INTO PlacaManifiesto(IDPlanPla, Numero) VALUES(@IDPlanPla, 1)";
                                using (SqlCommand cmdMani = new SqlCommand(queryMani, connection)) {
                                    cmdMani.Parameters.AddWithValue("@IDPlanPla", idPlanPla);
                                    await cmdMani.ExecuteNonQueryAsync();
                                }

                                string querySelect = "SELECT IDPlanMan FROM PlacaManifiesto WHERE IDPlanPla = @IDPlanPla";
                                using (SqlCommand cmdSelect = new SqlCommand(querySelect, connection)) {
                                    cmdSelect.Parameters.AddWithValue("@IDPlanPla", idPlanPla);

                                    var result = await cmdSelect.ExecuteScalarAsync();
                                    int idPlanMan = Convert.ToInt32(result);

                                    foreach (var pedido in placa.Value.Pedidos) {

                                        double cantidadDouble = double.Parse(pedido.cantidad);
                                        //double factorDouble = double.Parse(pedido.factor);
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
                                                        fabricante = reader["FirmName"].ToString();
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


                                        if (sl1code != "UBICACIÓN-DE-SsssswerwerwerssISTEMA") {
                                            string queryPlacaPedido = "INSERT INTO PlacaPedido (IDPlanMan, IDPlanPla, IDProducto, Cantidad, Peso, Descripcion, NumeroGuia, DocNum, LineNum, Ubicacion, MedidaBase, Fabricante, AbsEntry, SL1Code, SL2Code, SL3Code, SL4Code, Pesado, CodigoFabricante, Factor, CantidadBase, StockActual, Linea, CodigoBarras) VALUES (@IDPlanMan, @IDPlanPla,@IDProducto, @Cantidad, @Peso, @Descripcion, @NumeroGuia, @DocNum, @LineNum, @Ubicacion, @MedidaBase, @Fabricante, @AbsEntry, @Sl1Code, @Sl2Code, @Sl3Code, @Sl4Code, @TipoPeso, @CodigoFabricante, @Factor, @CantidadBase, @StockActual, @Linea, @CodigoBarras);";
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
                                                cmdPlaPedido.Parameters.AddWithValue("@Fabricante", fabricante);
                                                cmdPlaPedido.Parameters.AddWithValue("@AbsEntry", absentry);
                                                cmdPlaPedido.Parameters.AddWithValue("@SL1Code", sl1code);
                                                cmdPlaPedido.Parameters.AddWithValue("@SL2Code", sl2code);
                                                cmdPlaPedido.Parameters.AddWithValue("@SL3Code", sl3code);
                                                cmdPlaPedido.Parameters.AddWithValue("@SL4Code", sl4code);
                                                cmdPlaPedido.Parameters.AddWithValue("@TipoPeso", tipopeso);
                                                cmdPlaPedido.Parameters.AddWithValue("@CodigoFabricante", codigofabricante);
                                                cmdPlaPedido.Parameters.AddWithValue("@Factor", pedido.factor);
                                                cmdPlaPedido.Parameters.AddWithValue("@CantidadBase", cantidadmul);
                                                cmdPlaPedido.Parameters.AddWithValue("@StockActual", stockactual);
                                                cmdPlaPedido.Parameters.AddWithValue("@Linea", linea);
                                                cmdPlaPedido.Parameters.AddWithValue("@CodigoBarras", codebars);
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
                                string queryPedido = "INSERT INTO PedidosBeetrack (Codigo, Fecha, General, Planeado, Direccion, Cliente, Utilidad) VALUES (@Codigo, GETDATE(), @General, 1, @Direccion, @Cliente, @Utilidad)";
                                using (SqlCommand cmdPedido = new SqlCommand(queryPedido, connection)) {
                                    cmdPedido.Parameters.AddWithValue("@Codigo", pedido.ndocumento);
                                    cmdPedido.Parameters.AddWithValue("@General", codigo);
                                    cmdPedido.Parameters.AddWithValue("@Direccion", pedido.direccion);
                                    cmdPedido.Parameters.AddWithValue("@Cliente", pedido.nombrecontacto);
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




        [HttpPost]
        public ActionResult GuardarPlaneados(string codigos, int cc, int cs) {
            var total = cc + cs;
            try {
                Console.WriteLine(codigos);
                using (var connection = new SqlConnection(_connectionString)) {
                    connection.Open();

                    string sql1 = $@"
                            UPDATE PedidosBeetrack 
                            SET Planeado = 1 
                            WHERE Codigo IN ({codigos})
                        ";
                    using (var command = new SqlCommand(sql1, connection)) {
                        command.ExecuteNonQuery();
                    }

                    string sql2 = $@"
                            UPDATE CodigosBeetrack 
                            SET ExcelSubido = 1,
                            Totales = ${total},
                            Planeados = ${cc},
                            Libres = ${cs}
                            WHERE Codigo = (SELECT DISTINCT General FROM PedidosBeetrack WHERE Codigo IN ({codigos}) GROUP BY General)
                        ";
                    using (var command = new SqlCommand(sql2, connection)) {
                        command.ExecuteNonQuery();
                    }

                    connection.Close();
                }

                return Json(new { success = true, message = "Actualización de pedidos y códigos realizada correctamente." });
            } catch (Exception ex) {
                return Json(new { success = false, message = "Hubo un error al actualizar los pedidos y códigos.", error = ex.Message });
            }
        }


        [HttpPost]
        public async Task<IActionResult> GuardarConteo([FromBody] Dictionary<string, List<List<Registro>>> vehiculoDataConEstado, string codigobee) {  
            Console.WriteLine(codigobee);
            var connection = new SqlConnection(_connectionString);
            HanaConnection hanaConnection = new(_hanaConnectionString);

            List<string> errores = new List<string>();

            try {
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
                            errores.Add($"No se encontró el almacén para el usuario {usuario}.");
                            return StatusCode(500, new { message = "No se encontró el almacén para el usuario", errors = errores });
                        }
                    }
                }
                string insertPlanificacionQuery = "INSERT INTO Planificacion (Fecha, Almacen, Usuario, Clave) VALUES (GETDATE(), @Almacen, @Usuario, @Clave); SELECT SCOPE_IDENTITY();";
                SqlCommand planificacionCommand = new SqlCommand(insertPlanificacionQuery, connection);
                planificacionCommand.Parameters.AddWithValue("@Almacen", almacen);
                planificacionCommand.Parameters.AddWithValue("@Usuario", usuario);
                planificacionCommand.Parameters.AddWithValue("@Clave", clave);

                var idPlan = Convert.ToInt32(await planificacionCommand.ExecuteScalarAsync());

                Console.WriteLine($"Planificación ID: {idPlan} insertada.");
                string updateCodigosBeetrackQuery = "UPDATE CodigosBeetrack SET IDPlan = @IDPlan WHERE Codigo = @CodigoBee";
                SqlCommand updateCommand = new SqlCommand(updateCodigosBeetrackQuery, connection);
                updateCommand.Parameters.AddWithValue("@IDPlan", idPlan);
                updateCommand.Parameters.AddWithValue("@CodigoBee", codigobee);

                int rowsAffected = await updateCommand.ExecuteNonQueryAsync();


                foreach (var vehiculo in vehiculoDataConEstado) {
                    string placa = vehiculo.Key;
                    Console.WriteLine($"Insertando placa: {placa}");

                    string capacidad = null;

                    string queryCapacidad = $@"
                     SELECT ""U_EXX_PESVEH"" 
                        FROM ""@EXX_VEHICU"" 
                        WHERE ""Code"" = '{placa}'";
                    using (var hanaCommands = new HanaCommand(queryCapacidad, hanaConnection)) {
                        using (var readers = await hanaCommands.ExecuteReaderAsync()) {
                            if (await readers.ReadAsync()) {
                                capacidad = readers["U_EXX_PESVEH"].ToString();
                                Console.WriteLine($"Capacidad obtenida: {capacidad}");
                            } else {
                                errores.Add($"No se encontró la capacidad para el usuario {usuario}.");
                                return StatusCode(500, new { message = "No se encontró el almacén para el usuario", errors = errores });
                            }
                        }
                    }
                    string insertPlacaQuery = "INSERT INTO PlanificacionPlaca (IDPlan, Placa, Capacidad) VALUES (@IDPlan, @Placa, @Capacidad); SELECT SCOPE_IDENTITY();";
                    SqlCommand placaCommand = new SqlCommand(insertPlacaQuery, connection);
                    placaCommand.Parameters.AddWithValue("@IDPlan", idPlan);
                    placaCommand.Parameters.AddWithValue("@Placa", placa);
                    placaCommand.Parameters.AddWithValue("@Capacidad", capacidad);
                    var idPlanPla = Convert.ToInt32(await placaCommand.ExecuteScalarAsync());

                    Console.WriteLine($"Placa ID: {idPlanPla} insertada.");

                    for (int i = 0; i < vehiculo.Value.Count; i++) {
                        var listaRegistros = vehiculo.Value[i];

                        string insertManifiestoQuery = "INSERT INTO PlacaManifiesto (IDPlanPla, Numero) VALUES (@IDPlanPla, @Numero); SELECT SCOPE_IDENTITY();";
                        SqlCommand manifiestoCommand = new SqlCommand(insertManifiestoQuery, connection);
                        manifiestoCommand.Parameters.AddWithValue("@IDPlanPla", idPlanPla);
                        manifiestoCommand.Parameters.AddWithValue("@Numero", i + 1);
                        var idPlanMan = Convert.ToInt32(await manifiestoCommand.ExecuteScalarAsync());

                        Console.WriteLine($"Manifiesto ID: {idPlanMan} insertado.");

                        foreach (var registro in listaRegistros) {

                            var itemParts = registro.item.Split(new[] { '/' }, 2);
                            int cantidadmul = int.Parse(registro.cantidad);

                            string codigo = itemParts[0].Trim();
                            string descripcion = itemParts.Length > 1 ? itemParts[1].Trim() : string.Empty;

                            var codigoParts = codigo.Split('-');
                            string idProducto = codigoParts[0].Trim();
                            string lineNum = codigoParts.Length > 1 ? codigoParts[1].Trim() : string.Empty;
                            string factorStr = codigoParts.Length > 2 ? codigoParts[2].Trim() : "1";

                            int factor = int.TryParse(factorStr, out int parsedFactor) ? parsedFactor : 1;
                            int cantidadFinal = cantidadmul * factor;
                            decimal pesofinal = decimal.Parse(registro.peso) / factor;

                            string binCode = null;
                            string sl1code = null;
                            string sl2code = null;
                            string sl3code = null;
                            string sl4code = null;
                            string absentry = null;
                            string invntryUom = null;
                            string fabricante = null;
                            string tipopeso = null;
                            string codigofabricante = null;
                            string stockactual = null;
                            string linea = null;

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
                                    T4.""OnHand"",
                                    T5.""Name""
                                FROM OIBQ T0
                                INNER JOIN OBIN T1 ON T0.""BinAbs"" = T1.""AbsEntry""
                                INNER JOIN OITM T2 ON T2.""ItemCode"" = T0.""ItemCode""
                                INNER JOIN OMRC T3 ON T3.""FirmCode"" = T2.""FirmCode""
                                INNER JOIN OITW T4 ON T4.""ItemCode"" = T0.""ItemCode"" AND T4.""WhsCode"" = '{almacen}'
                                LEFT JOIN ""@EXF_SUBFAM"" T5 ON T5.""Code"" = T2.""U_FEM_SUBFAM""
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
                                            fabricante = reader["FirmName"].ToString();
                                            tipopeso = reader["U_FEM_TipoPeso"].ToString();
                                            codigofabricante = reader["SuppCatNum"].ToString();
                                            stockactual = reader["OnHand"].ToString();
                                            linea = reader["Name"].ToString();
                                            Console.WriteLine($"SAP: BinCode = {binCode}, InvntryUom = {invntryUom} para ItemCode = {idProducto}");
                                        } else {
                                            Console.WriteLine($"No se encontraron resultados en SAP para ItemCode = {idProducto}");
                                        }
                                    }
                                }
                            } catch (Exception sapEx) {
                                Console.WriteLine($"Error al consultar SAP para ItemCode = {idProducto}: {sapEx.Message}");
                                errores.Add($"Error al consultar SAP: {sapEx.Message}");
                            }
                            if (sl1code != "UBICACIÓN-DE-dfdfdfSIyrtSTEMA") {
                                string insertPedidoQuery = "INSERT INTO PlacaPedido (IDPlanMan, IDPlanPla, IDProducto, Cantidad, Peso, Descripcion, NumeroGuia, DocNum, LineNum, Ubicacion, MedidaBase, Fabricante, AbsEntry, SL1Code, SL2Code, SL3Code, SL4Code, Pesado, CodigoFabricante, Factor, CantidadBase, StockActual, Linea) VALUES (@IDPlanMan, @IDPlanPla,@IDProducto, @Cantidad, @Peso, @Descripcion, @NumeroGuia, @DocNum, @LineNum, @Ubicacion, @MedidaBase, @Fabricante, @AbsEntry, @Sl1Code, @Sl2Code, @Sl3Code, @Sl4Code, @TipoPeso, @CodigoFabricante, @Factor, @CantidadBase, @StockActual, @Linea);";

                                try {
                                    SqlCommand pedidoCommand = new SqlCommand(insertPedidoQuery, connection);
                                    pedidoCommand.Parameters.AddWithValue("@IDPlanMan", idPlanMan != 0 ? (object)idPlanMan : DBNull.Value);
                                    pedidoCommand.Parameters.AddWithValue("@IDPlanPla", idPlanPla != 0 ? (object)idPlanPla : DBNull.Value);
                                    pedidoCommand.Parameters.AddWithValue("@IDProducto", idProducto ?? (object)DBNull.Value);
                                    pedidoCommand.Parameters.AddWithValue("@Cantidad", cantidadFinal != 0 ? (object)cantidadFinal : DBNull.Value);
                                    pedidoCommand.Parameters.AddWithValue("@Peso", pesofinal != 0 ? (object)pesofinal : DBNull.Value);
                                    pedidoCommand.Parameters.AddWithValue("@Descripcion", string.IsNullOrEmpty(descripcion) ? DBNull.Value : descripcion);
                                    pedidoCommand.Parameters.AddWithValue("@NumeroGuia", string.IsNullOrEmpty(registro.numeroguia) ? DBNull.Value : registro.numeroguia);
                                    pedidoCommand.Parameters.AddWithValue("@DocNum", string.IsNullOrEmpty(registro.idsap) ? DBNull.Value : registro.idsap);
                                    pedidoCommand.Parameters.AddWithValue("@LineNum", lineNum ?? (object)DBNull.Value);
                                    pedidoCommand.Parameters.AddWithValue("@Ubicacion", string.IsNullOrEmpty(binCode) ? DBNull.Value : binCode);
                                    pedidoCommand.Parameters.AddWithValue("@MedidaBase", invntryUom ?? (object)DBNull.Value);
                                    pedidoCommand.Parameters.AddWithValue("@Fabricante", fabricante ?? (object)DBNull.Value);
                                    pedidoCommand.Parameters.AddWithValue("@AbsEntry", absentry ?? (object)DBNull.Value);
                                    pedidoCommand.Parameters.AddWithValue("@SL1Code", sl1code ?? (object)DBNull.Value);
                                    pedidoCommand.Parameters.AddWithValue("@SL2Code", sl2code ?? (object)DBNull.Value);
                                    pedidoCommand.Parameters.AddWithValue("@SL3Code", sl3code ?? (object)DBNull.Value);
                                    pedidoCommand.Parameters.AddWithValue("@SL4Code", sl4code ?? (object)DBNull.Value);
                                    pedidoCommand.Parameters.AddWithValue("@TipoPeso", tipopeso ?? (object)DBNull.Value);
                                    pedidoCommand.Parameters.AddWithValue("@CodigoFabricante", codigofabricante ?? (object)DBNull.Value);
                                    pedidoCommand.Parameters.AddWithValue("@Factor", factor != 0 ? (object)factor : DBNull.Value);
                                    pedidoCommand.Parameters.AddWithValue("@CantidadBase", registro.cantidad ?? (object)DBNull.Value);
                                    pedidoCommand.Parameters.AddWithValue("@StockActual", stockactual ?? (object)DBNull.Value);
                                    pedidoCommand.Parameters.AddWithValue("@Linea", linea ?? (object)DBNull.Value);


                                    await pedidoCommand.ExecuteNonQueryAsync();
                                    Console.WriteLine($"Pedido insertado: IDPlanMan = {idPlanMan}, IDProducto = {idProducto}, Cantidad = {registro.cantidad}, Peso = {registro.peso}, Ubicacion = {binCode}, MedidaBase = {invntryUom}");
                                } catch (Exception ex) {
                                //    errores.Add($"Error al insertar en PlacaPedido: {ex.Message}");
                                    Console.WriteLine($"Error al insertar en PlacaPedido: {ex.Message}");
                                }
                            } else {
                                Console.WriteLine($"No se realiza el insert porque SL1Code es 'UBICACIÓN-DE-SIsdasdsSTEMA'.");
                            }
                        }
                    }
                }

                if (errores.Count > 0) {
                    return StatusCode(500, new { message = "Se encontraron errores durante la inserción.", errors = errores });
                }

                return Ok(new { message = "Registros guardados en la base de datos." });
            } catch (Exception ex) {
                return StatusCode(500, new { message = "Ocurrió un error al procesar el conteo.", error = ex.Message });
            } finally {
                if (hanaConnection.State == System.Data.ConnectionState.Open) {
                    await hanaConnection.CloseAsync();
                }
            }
        }

        public async Task<IActionResult> ActualizarEntregaSAP(string ndocumento, string nuevaFecha) {
            ObtenerUsuarioYClave();
            string sessionId = null;
            HttpClientHandler clientHandlerl = new HttpClientHandler();
            clientHandlerl.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => { return true; };

            using (var httpClient = new HttpClient(clientHandlerl)) {
                var json = JsonConvert.SerializeObject(new {
                    CompanyDB = "FEMACO_PROD",
                    Password = clave,
                    UserName = usuario
                });

                var httpContentl = new StringContent(json);
                httpContentl.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                var responsel = await httpClient.PostAsync("https://192.168.1.9:50000/b1s/v1/Login", httpContentl);

                if (responsel.IsSuccessStatusCode) {
                    var responseContentl = await responsel.Content.ReadAsStringAsync();
                    dynamic responseObject = JsonConvert.DeserializeObject(responseContentl);
                    sessionId = responseObject.SessionId;
                    Console.WriteLine(responseContentl);
                } else {
                    Console.WriteLine($"Error en la solicitud: {responsel.StatusCode}");
                    return Json(new { success = false, message = "Error en la solicitud de login" });
                }
            }

            try {
                HttpClientHandler clientHandler = new HttpClientHandler();
                clientHandler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => { return true; };

                using (var httpClient = new HttpClient(clientHandler)) {
                    var json = JsonConvert.SerializeObject(new {
                        DocDueDate = nuevaFecha
                    });
                    Console.WriteLine("JSON que se enviará en la solicitud PATCH:");
                    Console.WriteLine(json);

                    var httpContent = new StringContent(json);
                    httpContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                    httpClient.DefaultRequestHeaders.Add("Cookie", "B1SESSION=" + sessionId);

                    var response = await httpClient.PatchAsync("https://192.168.1.9:50000/b1s/v1/Orders(" + ndocumento + ")", httpContent);

                    if (response.IsSuccessStatusCode) {
                        var responseContent = await response.Content.ReadAsStringAsync();
                        Console.WriteLine(responseContent + " - Actualización exitosa");
                        return Json(new { success = true, message = "Fecha de vencimiento actualizada correctamente" });
                    } else {
                        var responseContent = await response.Content.ReadAsStringAsync();
                        dynamic jsonResponse = JsonConvert.DeserializeObject(responseContent);
                        string errorMessage = jsonResponse.error.message.value;
                        Console.WriteLine($"Error en la solicitud: {ndocumento} - {errorMessage}");
                        return Json(new { success = false, message = errorMessage });
                    }
                }
            } catch (Exception ex) {
                Console.WriteLine($"Error al actualizar el documento: {ex.Message}");
                return Json(new { success = false, message = $"Error al actualizar el documento: {ex.Message}" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerPlanificacionesEnviadas() {

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
				(SELECT COUNT(DISTINCT P1.IDProducto) FROM PlacaPedido P1 WHERE P1.IDPlanPla = T3.IDPlanPla) AS TotalItems,
				(SELECT SUM(P4.Factor*P4.Cantidad) FROM PickeoProductoIngresado P4 INNER JOIN PickeoProducto P5 ON P4.IDPProducto = P5.IDPProducto WHERE P5.IDPlaca = T3.IDPlanPla) AS PickadosReal,
				(SELECT SUM(P0.Cantidad) FROM PlacaPedido P0 WHERE P0.IDPlanPla = T3.IDPlanPla) AS TotalPick,
                (SELECT COUNT(P1.IDProducto) FROM PickeoProducto P1 WHERE P1.IDPlaca = T3.IDPlanPla) AS Items,
                (SELECT COUNT(P1.IDProducto) FROM PickeoProducto P1 WHERE P1.IDPlaca = T3.IDPlanPla AND P1.Finalizado = 1) AS Finalizados,
                (SELECT COUNT(P1.IDProducto) FROM PlacaPedido P1 WHERE P1.IDPlanPla = T3.IDPlanPla AND P1.RevisadoCoor = 1) AS RevisadosCoor,
                (SELECT COUNT(P1.IDProducto) FROM PlacaPedido P1 WHERE P1.IDPlanPla = T3.IDPlanPla) AS Pedidos,
                (SELECT TOP 1 P2.IDPick FROM PlacaManifiesto P2 WHERE P2.IDPlanPla = T3.IDPlanPla) AS IDPicks,
                (SELECT SUM(P3.Cantidad*P3.Peso) FROM PlacaPedido P3 WHERE P3.IDPlanPla = T3.IDPlanPla) AS PesoCarga,
				(SELECT SUM(P1.Peso*P1.Cantidad) FROM PlacaPedido P1 INNER JOIN PickeoProducto P2 ON P2.IDPlaca = P1.IDPlanPla  AND P2.IDProducto = P1.IDProducto WHERE P1.IDPlanPla = T3.IDPlanPla AND P2.IDPick = T4.IDPick) AS PesoTotalPick,
                (SELECT COUNT(P2.IDProducto) FROM PickeoProducto P2 WHERE P2.IDPlaca = T3.IDPlanPla AND P2.IDPick = T4.IDPick) AS TotalItemsPick,
                T7.Pendientes,
                (SELECT SUM(P1.Cantidad) FROM PlacaPedido P1 INNER JOIN PickeoProducto P2 ON P2.IDProducto = P1.IDProducto WHERE P1.IDPlanPla = T3.IDPlanPla AND P2.IDPick = T4.IDPick) AS Acontar,
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
                LEFT JOIN Planificacion T2 ON T2.IDPlan = T1.IDPlan
                LEFT JOIN PlanificacionPlaca T3 ON T2.IDPlan = T3.IDPlan
                LEFT JOIN PickeoProducto T4 ON T4.IDPlaca = T3.IDPlanPla
                LEFT JOIN PickeoPersonal T5 ON T5.IDPP = T4.IDPick
                LEFT JOIN PickeoPersonal T6 ON T6.IDPP = T3.Usuario
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
                GROUP BY R1.IDPlanPla) T7 ON T7.IDPlanPla = T3.IDPlanPla
                LEFT JOIN PickeoProductoIngresado T8 ON T8.IDPProducto = T4.IDPProducto
                WHERE T1.Almacen = @almacen  and ((T1.Tipo='Local' and T3.LastMileCodigo IS NULL)OR(T1.Tipo='Provincia'))
                GROUP BY T1.IDPlan, T1.Tipo, T1.Codigo, T1.Fecha, T1.Totales, T1.Planeados, T1.Libres,
                T1.ExcelSubido, T1.Almacen, T3.Placa, T3.Capacidad, T3.IDPlanPla, T3.Usuario, T4.IDPick, T5.Nombre, T6.Nombre,
                T7.Pendientes, T3.Confirmado,T3.CargaIncompleta,T3.Enviado,T3.Cargar,T3.Cargado,T3.Revision,T3.Sap, T3.LastMileCodigo,
                T3.FechaInicio, T3.FechaFin ORDER BY Fecha ";

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
                if ((row.Tipo == "Local" && row.LastMileCodigo == null) || (row.Tipo == "Provincia" && row.Manifiesto== "SIN MANIFIESTO") ) {
                    filteredResult.Add(row);
                }
                Console.WriteLine("manifiesto: " + row.Manifiesto);

            }

            await hanaConnection.CloseAsync();
            await connection.CloseAsync();

            return Ok(filteredResult);
        }


        [HttpGet]
        public async Task<IActionResult> ObtenerPlanificacionesHistorial() {

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
				(SELECT COUNT(DISTINCT P1.IDProducto) FROM PlacaPedido P1 WHERE P1.IDPlanPla = T3.IDPlanPla) AS TotalItems,
				(SELECT SUM(P4.Factor*P4.Cantidad) FROM PickeoProductoIngresado P4 INNER JOIN PickeoProducto P5 ON P4.IDPProducto = P5.IDPProducto WHERE P5.IDPlaca = T3.IDPlanPla) AS PickadosReal,
				(SELECT SUM(P0.Cantidad) FROM PlacaPedido P0 WHERE P0.IDPlanPla = T3.IDPlanPla) AS TotalPick,
                (SELECT COUNT(P1.IDProducto) FROM PickeoProducto P1 WHERE P1.IDPlaca = T3.IDPlanPla) AS Items,
                (SELECT COUNT(P1.IDProducto) FROM PickeoProducto P1 WHERE P1.IDPlaca = T3.IDPlanPla AND P1.Finalizado = 1) AS Finalizados,
                (SELECT COUNT(P1.IDProducto) FROM PlacaPedido P1 WHERE P1.IDPlanPla = T3.IDPlanPla AND P1.RevisadoCoor = 1) AS RevisadosCoor,
                (SELECT COUNT(P1.IDProducto) FROM PlacaPedido P1 WHERE P1.IDPlanPla = T3.IDPlanPla) AS Pedidos,
                (SELECT TOP 1 P2.IDPick FROM PlacaManifiesto P2 WHERE P2.IDPlanPla = T3.IDPlanPla) AS IDPicks,
                (SELECT SUM(P3.Cantidad*P3.Peso) FROM PlacaPedido P3 WHERE P3.IDPlanPla = T3.IDPlanPla) AS PesoCarga,
				(SELECT SUM(P1.Peso*P1.Cantidad) FROM PlacaPedido P1 INNER JOIN PickeoProducto P2 ON P2.IDPlaca = P1.IDPlanPla  AND P2.IDProducto = P1.IDProducto WHERE P1.IDPlanPla = T3.IDPlanPla AND P2.IDPick = T4.IDPick) AS PesoTotalPick,
                (SELECT COUNT(P2.IDProducto) FROM PickeoProducto P2 WHERE P2.IDPlaca = T3.IDPlanPla AND P2.IDPick = T4.IDPick) AS TotalItemsPick,
                T7.Pendientes,
                (SELECT SUM(P1.Cantidad) FROM PlacaPedido P1 INNER JOIN PickeoProducto P2 ON P2.IDProducto = P1.IDProducto WHERE P1.IDPlanPla = T3.IDPlanPla AND P2.IDPick = T4.IDPick) AS Acontar,
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
                LEFT JOIN Planificacion T2 ON T2.IDPlan = T1.IDPlan
                LEFT JOIN PlanificacionPlaca T3 ON T2.IDPlan = T3.IDPlan
                LEFT JOIN PickeoProducto T4 ON T4.IDPlaca = T3.IDPlanPla
                LEFT JOIN PickeoPersonal T5 ON T5.IDPP = T4.IDPick
                LEFT JOIN PickeoPersonal T6 ON T6.IDPP = T3.Usuario
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
                GROUP BY R1.IDPlanPla) T7 ON T7.IDPlanPla = T3.IDPlanPla
                LEFT JOIN PickeoProductoIngresado T8 ON T8.IDPProducto = T4.IDPProducto
                WHERE T1.Almacen = @almacen 
                GROUP BY T1.IDPlan, T1.Tipo, T1.Codigo, T1.Fecha, T1.Totales, T1.Planeados, T1.Libres,
                T1.ExcelSubido, T1.Almacen, T3.Placa, T3.Capacidad, T3.IDPlanPla, T3.Usuario, T4.IDPick, T5.Nombre, T6.Nombre,
                T7.Pendientes, T3.Confirmado,T3.CargaIncompleta,T3.Enviado,T3.Cargar,T3.Cargado,T3.Revision,T3.Sap, T3.LastMileCodigo,
                T3.FechaInicio, T3.FechaFin ORDER BY Fecha";

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
                if ((row.Tipo == "Local" && row.LastMileCodigo != null) || (row.Tipo == "Provincia" && row.Manifiesto != "SIN MANIFIESTO")) {
                    filteredResult.Add(row);
                }
            }

            await hanaConnection.CloseAsync();
            await connection.CloseAsync();
            return Ok(filteredResult);
        }

        [HttpDelete]
        public ActionResult BorrarSubida(string codigo) {
            try {
                using (var connection = new SqlConnection(_connectionString)) {
                    connection.Open();

                    string deletePedidosQuery = "DELETE FROM PedidosBeetrack WHERE General = @Codigo";
                    using (var command = new SqlCommand(deletePedidosQuery, connection)) {
                        command.Parameters.AddWithValue("@Codigo", codigo);
                        command.ExecuteNonQuery();
                    }

                    string deleteCodigosQuery = "DELETE FROM CodigosBeetrack WHERE Codigo = @Codigo";
                    using (var command = new SqlCommand(deleteCodigosQuery, connection)) {
                        command.Parameters.AddWithValue("@Codigo", codigo);
                        command.ExecuteNonQuery();
                    }

                    return Json(new { success = true, message = "Subida borrada correctamente." });
                }
            } catch (Exception ex) {
                return Json(new { success = false, message = "Hubo un error al borrar la subida.", error = ex.Message });
            }
        }


        [HttpDelete]
        public ActionResult BorrarSubidaProvincia(string codigo, int plan) {
            try {
                using (var connection = new SqlConnection(_connectionString)) {
                    connection.Open();

                    string deletePedidosQuery = "DELETE FROM PedidosBeetrack WHERE General = @Codigo";
                    using (var command = new SqlCommand(deletePedidosQuery, connection)) {
                        command.Parameters.AddWithValue("@Codigo", codigo);
                        command.ExecuteNonQuery();
                    }

                    string deleteCodigosQuery = "DELETE FROM CodigosBeetrack WHERE Codigo = @Codigo";
                    using (var command = new SqlCommand(deleteCodigosQuery, connection)) {
                        command.Parameters.AddWithValue("@Codigo", codigo);
                        command.ExecuteNonQuery();
                    }
                    string deleteplanificacionQuery = "DELETE FROM Planificacion WHERE IDPlan = (SELECT IDPlan FROM PlanificacionPlaca WHERE IDPlanPla = @Plan)";
                    using (var command = new SqlCommand(deleteplanificacionQuery, connection)) {
                        command.Parameters.AddWithValue("@Plan", plan);
                        command.ExecuteNonQuery();
                    }
                    string deleteplanificacionplaQuery = "DELETE FROM PlanificacionPlaca WHERE IDPlanPla = @Plan";
                    using (var command = new SqlCommand(deleteplanificacionplaQuery, connection)) {
                        command.Parameters.AddWithValue("@Plan", plan);
                        command.ExecuteNonQuery();
                    }
                    string deleteplanificacionmanQuery = "DELETE FROM PlacaManifiesto WHERE IDPlanPla = @Plan";
                    using (var command = new SqlCommand(deleteplanificacionmanQuery, connection)) {
                        command.Parameters.AddWithValue("@Plan", plan);
                        command.ExecuteNonQuery();
                    }

                    string deleteplanificacionpedidoQuery = "DELETE FROM PlacaPedido WHERE IDPlanPla = @Plan";
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

        [HttpGet]
        public async Task<IActionResult> ObtenerPedidosCodigo(string codigo) {
            using (var connection = new SqlConnection(_connectionString)) {
                string query = $@"
                        SELECT * FROM PedidosBeetrack WHERE General = '{codigo}'";

                var result = await connection.QueryAsync(query);
                return Ok(result);
            }
        }


        [HttpGet]
        public async Task<IActionResult> ObtenerPedidosSinCodigo(string codigo) {
            using (var connection = new SqlConnection(_connectionString)) {
                string query = $@"
                        SELECT * FROM PedidosBeetrack WHERE General = {codigo} AND Planeado IS NULL";

                var result = await connection.QueryAsync(query);
                return Ok(result);
            }
        }

        [HttpGet]
        public async Task<IActionResult> CargarPlacas() {
            var connection = new SqlConnection(_connectionString);
            HanaConnection hanaConnection = new(_hanaConnectionString);

            await hanaConnection.OpenAsync();
            await connection.OpenAsync();
            ObtenerUsuarioYClave();
            string almacen = null;

            string sappQuery = $@"
                SELECT ""Warehouse"" FROM OUDG WHERE ""Code"" = '{usuario}' ";
            using (var hanaCommand = new HanaCommand(sappQuery, hanaConnection)) {
                using (var reader = await hanaCommand.ExecuteReaderAsync()) {
                    if (await reader.ReadAsync()) {
                        almacen = reader["Warehouse"].ToString();
                    } else {
                        return StatusCode(500, new { message = "No se encontró el almacén para el usuario" });
                    }
                }

                string query = $@"
                    SELECT ""U_EXX_PESVEH"" AS Peso, ""Code""
                    FROM ""@EXX_VEHICU"" WHERE ""U_FEM_DISBT"" = '{almacen}'";

                var result = await hanaConnection.QueryAsync<VehiculoModel>(query);

                return Ok(result);
            }
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerPedidosPlaca(string codigo) {
            using (var connection = new SqlConnection(_connectionString)) {
                string query = $@"
                    SELECT 
                        T0.NumeroGuia,
                        T4.Direccion,
                        T4.Cliente,
                        SUM(T0.Peso*T0.Cantidad) AS Peso,
	                    (SELECT COUNT(P1.NumeroGuia) FROM PlacaPedido P1 WHERE P1.NumeroGuia = T0.NumeroGuia) AS Items
                        FROM PlacaPedido T0
                        INNER JOIN PlanificacionPlaca T1 ON T1.IDPlanPla = T0.IDPlanPla
                        INNER JOIN Planificacion T2 ON T2.IDPlan = T1.IDPlan
                        INNER JOIN CodigosBeetrack T3 ON T3.IDPlan = T2.IDPlan
                        INNER JOIN PedidosBeetrack T4 ON T4.General = T3.Codigo AND T4.Codigo = T0.NumeroGuia
                        WHERE T0.IDPlanPla = {codigo}
                        GROUP BY T0.NumeroGuia, T4.Direccion, T4.Cliente";

                var result = await connection.QueryAsync(query);
                return Ok(result);
            }
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerProductosPedidosPlaca(int ID) {
            using (var connection = new SqlConnection(_connectionString)) {
                string query = @"
                    SELECT 
                    T3.IDProducto,
                    SUM(T3.Cantidad) AS TotalCantidad,
                    T3.Descripcion,
                    T3.MedidaBase,
                    T3.Fabricante,
                    T3.Pesado,
                    T3.SL1Code,
                    T3.SL2Code,
                    T3.SL3Code,
                    T3.SL4Code,
                    (SELECT SUM(P1.Peso*P1.Cantidad) FROM PlacaPedido P1 WHERE P1.IDPlanPla = T1.IDPlanPla AND P1.IDProducto = T3.IDProducto) AS PesoTotal,
                    (SELECT COUNT(P1.IDPProducto) FROM PickeoProducto P1 WHERE P1.IDPlaca = T1.IDPlanPla) AS Iniciados
                    FROM Planificacion T0
                    INNER JOIN PlanificacionPlaca T1 ON T0.IDPlan = T1.IDPlan
                    INNER JOIN PlacaManifiesto T2 ON T1.IDPlanPla = T2.IDPlanPla
                    INNER JOIN PlacaPedido T3 ON T2.IDPlanMan = T3.IDPlanMan
                    LEFT JOIN PickeoProducto T4 ON T4.IDProducto = T3.IDProducto AND T4.IDPlaca = T1.IDPlanPla
                    WHERE 
                    T1.IDPlanPla = @IDPlan
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
                    T3.SL4Code
                    ORDER BY Pesado, SL1Code, SL2Code, SL3Code, SL4Code";

                var parameters = new { IDPlan = ID };
                var result = await connection.QueryAsync<ProductoPlan>(query, parameters);
                return Ok(result);
            }
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerProductosPedidosFijo(int idplan, string idproducto) {
            using (var connection = new SqlConnection(_connectionString)) {
                string query = @"
                    SELECT 
                    T0.IDPlanPed, 
                    T0.Descripcion, 
                    T0.Cantidad, 
                    T0.CantidadBase, 
                    T0.Factor,
                    T0.Peso, 
                    T0.IDProducto, 
                    T0.NumeroGuia ,
                    T2.Utilidad
                    FROM PlacaPedido T0 
                    INNER JOIN CodigosBeetrack T1 ON T1.IDPlan = T0.IDPlanPla
                    INNER JOIN PedidosBeetrack T2 ON T2.General = T1.Codigo AND T2.Codigo = T0.NumeroGuia 
                    WHERE 
                    T0.IDPlanPla = @idplan 
                    AND T0.IDProducto = @idproducto";

                var parameters = new { idplan, idproducto };
                var result = await connection.QueryAsync<PedidosProductoFijo>(query, parameters);
                return Ok(result);
            }
        }

        [HttpPost]
        public async Task<IActionResult> GuardarProductoPedidoCodigo([FromBody] List<ProductoPedidoDto> productosPedidos) {
            using (var connection = new SqlConnection(_connectionString)) {
                string query = @"
                    UPDATE PlacaPedido 
                    SET CantidadBase = @nuevaCantidadBase, 
                        Cantidad = @nuevacantidad
                    WHERE IDPlanPed = @planpedido AND IDProducto = @idProducto";

                foreach (var productoPedido in productosPedidos) {
                    var parameters = new {
                        nuevaCantidadBase = productoPedido.nuevaCantidadBase,
                        maxCantidadBase = productoPedido.nuevaCantidadBase,
                        nuevacantidad = productoPedido.nuevaCantidadBase * Convert.ToDecimal(productoPedido.factor),
                        planpedido = productoPedido.planpedido,
                        idProducto = productoPedido.idProducto
                    };

                    try {
                        var result = await connection.ExecuteAsync(query, parameters);

                        if (result == 0) {
                            return BadRequest(new { message = "No se pudo actualizar el producto con IDProducto: " + productoPedido.idProducto });
                        }
                    } catch (Exception ex) {
                        return StatusCode(500, new { message = "Error al actualizar el producto", error = ex.Message });
                    }
                }

                return Ok(new { success = true, message = "Productos actualizados correctamente" });
            }
        }

        [HttpPost]
        public IActionResult ProcesarCodigo(string codigo) {
            if (!string.IsNullOrEmpty(codigo)) {
                return Json(new { success = true, mensaje = "Código procesado: " + codigo });
            }
            return Json(new { success = false, mensaje = "No se recibió ningún código." });
        }

        //[HttpGet]
        //public IActionResult ObtenerCodigoSap(string itemcode) {
        //    var str = """
        //        SELECT 
        //            T0."ItemCode", 
        //            T0."ItemName",
        //            T0."CodeBars"
        //        FROM OITM T0
        //        WHERE T0."ItemCode" LIKE '%
        //        """ + itemcode + """
        //        %'
        //        OR T0."CodeBars" = '
        //        """ + itemcode + """
        //        '
        //        """;
        //    Console.WriteLine(str);

        //    using (var hanaConnection = new HanaConnection(_hanaConnectionString)) {
        //        hanaConnection.Open();

        //        var resultado = hanaConnection.Query<ItemInfoSap>(str);

        //        hanaConnection.Close();

        //        return Ok(resultado);
        //    }
        //}
        [HttpGet]
        public IActionResult ObtenerCodigoSap(string itemcode) {
            var str = """
                SELECT DISTINCT
                    T0."ItemCode",
                    T0."ItemName",
                    T0."CodeBars"
                FROM OITM T0
                LEFT JOIN OBCD T1 ON T0."ItemCode" = T1."ItemCode"
                WHERE T0."ItemCode" LIKE '%
                """ + itemcode + """
                %'
                OR T1."BcdCode" = '
                """ + itemcode + """
                '
                """;

            using (var hanaConnection = new HanaConnection(_hanaConnectionString)) {
                hanaConnection.Open();

                var resultado = hanaConnection.Query<ItemInfoSap>(str);

                hanaConnection.Close();

                return Ok(resultado);
            }
        }

        [HttpGet]
        public IActionResult ObtenerPaqueteriasSap(string itemCode) {
            var str = $@"
                SELECT DISTINCT T1.""UomName"" AS Medida, T1.""UomCode"" AS IdMedida, 1.0 AS Factor, T1.""UomEntry"", T2.""BcdCode"", T2.""BcdEntry""
                FROM ITM1 T0
                INNER JOIN OUOM T1 ON T1.""UomEntry"" = T0.""UomEntry""
                LEFT JOIN OBCD T2 ON T2.""ItemCode"" = T0.""ItemCode"" AND T2.""UomEntry"" = T1.""UomEntry""
                WHERE T0.""ItemCode"" = '{itemCode}'
                UNION ALL
                SELECT DISTINCT T1.""UomName"" AS Medida, T1.""UomCode"" AS IdMedida, T2.""BaseQty"" AS Factor, T1.""UomEntry"", T3.""BcdCode"", T3.""BcdEntry""
                FROM ITM9 T0
                INNER JOIN OUOM T1 ON T1.""UomEntry"" = T0.""UomEntry""
                INNER JOIN UGP1 T2 ON T2.""UomEntry"" = T1.""UomEntry""
                LEFT JOIN OBCD T3 ON T3.""ItemCode"" = T0.""ItemCode"" AND T3.""UomEntry"" = T1.""UomEntry""
                WHERE T0.""ItemCode"" = '{itemCode}'
                ORDER BY Factor ";
            Console.WriteLine(str);
            try {
                using (var hanaConnection = new HanaConnection(_hanaConnectionString)) {
                    hanaConnection.Open();

                    var resultado = hanaConnection.Query<PaqueteriaInfoSap>(str);

                    hanaConnection.Close();

                    return Ok(resultado);
                }
            } catch (Exception ex) {
                return StatusCode(500, "Error al obtener las paqueterías: " + ex.Message);
            }
        }

        [HttpPost]
        public async Task<IActionResult> GuardarCodigoBarrasSap([FromBody] CodigoBarrasRequest request, string num) {
            try {
                ObtenerUsuarioYClave();
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

                    if (!loginResponse.IsSuccessStatusCode) {
                        return BadRequest("Error al realizar el login en SAP.");
                    }

                    var responseContent = await loginResponse.Content.ReadAsStringAsync();
                    dynamic responseObject = JsonConvert.DeserializeObject(responseContent);
                    string sessionId = responseObject.SessionId;
                    Console.WriteLine("ESTE ES LA SESSION: " + sessionId);
                    if (string.IsNullOrEmpty(sessionId)) {
                        return BadRequest("No se pudo obtener el SessionId de SAP.");
                    }


                    var jsonContent = new {
                        Barcode = request.Barcode,
                        FreeText = "",
                        ItemNo = request.ItemCode,
                        UoMEntry = request.UomEntry
                    };

                    string jsonString = JsonConvert.SerializeObject(jsonContent);
                    Console.WriteLine("JSON enviado a SAP: " + jsonString);

                    httpClient.DefaultRequestHeaders.Add("Cookie", "B1SESSION=" + sessionId);

                    var postContent = new StringContent(JsonConvert.SerializeObject(jsonContent), Encoding.UTF8, "application/json");

                    var postResponse = await httpClient.PostAsync("https://192.168.1.9:50000/b1s/v1/BarCodes", postContent);

                    if (num == "1") {
                        var patchContent = new {
                            BarCode = request.Barcode
                        };

                        var patchJson = JsonConvert.SerializeObject(patchContent);
                        string itemCodeUrl = $"https://192.168.1.9:50000/b1s/v1/Items('{request.ItemCode}')";
                        var patchResponse = await httpClient.PatchAsync(itemCodeUrl, new StringContent(patchJson, Encoding.UTF8, "application/json"));

                        if (!patchResponse.IsSuccessStatusCode) {
                            return StatusCode(500, "Error al actualizar el item en SAP.");
                        }
                    }


                    if (postResponse.IsSuccessStatusCode) {
                        return Ok(new { success = true });
                    } else {
                        return StatusCode(500, "Error al guardar el código de barras.");
                    }
                }
            } catch (Exception ex) {
                return StatusCode(500, "Error al procesar la solicitud: " + ex.Message);
            }
        }

        [HttpPost]
        public async Task<IActionResult> ActualizarCodigoBarrasSap([FromBody] ActualizarCodigoBarrasRequest request, string num) {
            try {
                ObtenerUsuarioYClave();

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

                    if (!loginResponse.IsSuccessStatusCode) {
                        return BadRequest("Error al realizar el login en SAP.");
                    }

                    var responseContent = await loginResponse.Content.ReadAsStringAsync();
                    dynamic responseObject = JsonConvert.DeserializeObject(responseContent);
                    string sessionId = responseObject.SessionId;

                    if (string.IsNullOrEmpty(sessionId)) {
                        return BadRequest("No se pudo obtener el SessionId de SAP.");
                    }

                    var jsonContent = new {
                        Barcode = request.Barcode
                    };

                    string jsonString = JsonConvert.SerializeObject(jsonContent);

                    httpClient.DefaultRequestHeaders.Add("Cookie", "B1SESSION=" + sessionId);

                    var patchRequest = new HttpRequestMessage(new HttpMethod("PATCH"),
                        $"https://192.168.1.9:50000/b1s/v1/BarCodes({request.BcdEntry})") {
                        Content = new StringContent(jsonString, Encoding.UTF8, "application/json")
                    };

                    var patchResponse = await httpClient.SendAsync(patchRequest);


                    if (num == "1") {
                        var patchContent = new {
                            BarCode = request.Barcode
                        };

                        var patchJson = JsonConvert.SerializeObject(patchContent);
                        string itemCodeUrl = $"https://192.168.1.9:50000/b1s/v1/Items('{request.ItemCode}')";
                        var patchResponses = await httpClient.PatchAsync(itemCodeUrl, new StringContent(patchJson, Encoding.UTF8, "application/json"));

                        if (!patchResponses.IsSuccessStatusCode) {
                            return StatusCode(500, "Error al actualizar el item en SAP.");
                        }
                    }


                    if (patchResponse.IsSuccessStatusCode) {
                        return Ok(new { success = true });
                    } else {
                        return StatusCode(500, "Error al actualizar el código de barras.");
                    }
                }
            } catch (Exception ex) {
                return StatusCode(500, "Error al procesar la solicitud: " + ex.Message);
            }
        }


        public IActionResult Privacy() {
            return View();
        }
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error() {
            return null;
        }
    }
}