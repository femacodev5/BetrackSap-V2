using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using MorosidadWeb.Data;
using MorosidadWeb.Models;
using Sap.Data.Hana;
using System.Collections;
using System.Data;
using System.Diagnostics;
using MySql.Data.MySqlClient;
using Newtonsoft.Json.Linq;
using SAPbobsCOM;
using static System.Runtime.InteropServices.JavaScript.JSType;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using NPOI.HSSF.UserModel;
using MorosidadWeb.Models.ViewModels;
using Newtonsoft.Json;
using System.Dynamic;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using Org.BouncyCastle.Tls;
using MySqlX.XDevAPI;
using System.Security.Claims;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace MorosidadWeb.Controllers {
    [Authorize]
    public class ScoreBoardController : Controller {
        private readonly ILogger<HomeController> _logger;
        private readonly DapperContext _context;

        private string usuario;
        private string clave;

        private Company _company;
        private int error;
        public ScoreBoardController(ILogger<HomeController> logger, DapperContext context) {
            _logger = logger;
            _context = context;
        }
        //public IActionResult Index() => View();
        public IActionResult Index() {
            ObtenerUsuarioYClave();

            List<SelectListItem> territorios = ObtenerTerritorios();
            List<SelectListItem> vendedores = ObtenerVendedores();
            List<SelectListItem> zonarep = ObtenerZonarep();

            ViewBag.Territorios = territorios;
            ViewBag.Vendedores = vendedores;
            ViewBag.Zonarep = zonarep;

            return View();
        }
        private void ObtenerUsuarioYClave() {
            string userDataJson = User.FindFirstValue(ClaimTypes.UserData);

            if (!string.IsNullOrEmpty(userDataJson)) {
                dynamic user = JsonConvert.DeserializeObject<dynamic>(userDataJson);
                usuario = user.usuario;
                clave = user.clave;
                Console.WriteLine("Usuario: " + usuario);
                Console.WriteLine("Clave: " + clave);
            } else {
                Console.WriteLine("No se encontraron datos de ClaimTypes.UserData.");
            }
        }
        public record MorosidadDTO(int Id, string Detalle, bool Aprobado);

        public List<SelectListItem> ObtenerTerritorios() {
            List<SelectListItem> territorios = new List<SelectListItem>();
            var hanaConnection = new HanaConnection("server=192.168.1.9:30015;CurrentSchema=FEMACO_PROD;userid=SAPINST;password=F3m4c0H4n4");
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
            var hanaConnection = new HanaConnection("server=192.168.1.9:30015;CurrentSchema=FEMACO_PROD;userid=SAPINST;password=F3m4c0H4n4");
            hanaConnection.Open();
            if (hanaConnection.State == ConnectionState.Open) {
                string query = """SELECT "SlpCode", "SlpName" FROM OSLP WHERE "Active" = 'Y'""";
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
            var hanaConnection = new HanaConnection("server=192.168.1.9:30015;CurrentSchema=FEMACO_PROD;userid=SAPINST;password=F3m4c0H4n4");
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

            string query = @"SELECT T0.PKID as IDPro, T0.Placa, 
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
        public async Task<IActionResult> VentaConConsolidado(string desde, string hasta, string desdevenc, string hastavenc, string rpt, string terri, string znrpt) {
            string text = "" + rpt + "";
            string textzn = "" + znrpt + "";
            int length = text.Length;
            int lengthzn = textzn.Length;
            Console.WriteLine("The length of the string is: " + length);
            var hanaConnection = new HanaConnection("server=192.168.1.9:30015;CurrentSchema=FEMACO_PROD;userid=SAPINST;password=F3m4c0H4n4");
            hanaConnection.Open();
            if (lengthzn == 0) {
                if (length == 0) {
                    var str = """
                SELECT
                T1."DocNum" AS "NDOCUMENTO",
                T1."DocEntry" AS "ENTRY",
                T2."LineNum" AS "POSICI",
                T3."U_XM_LatitudS" AS "LATITUD",
                T3."U_XM_LongitudS" AS "LONGITUD",
                T3."StreetS" AS "DIRECCION",
                T2."Dscription" AS "NOMBREITEM",
                T2."Quantity" AS "CANTIDAD",
                T2."ItemCode" AS "CODIGOITEM",
                TO_VARCHAR (TO_DATE(T2."ShipDate"), 'DD/MM/YYYY') AS "FECHAMINENTREGA",
                TO_VARCHAR (TO_DATE(T1."DocDueDate"), 'DD/MM/YYYY') AS "FECHAMAXENTREGA",
                '07:30' AS "MINVENTANAHORARIA1",
                '20:30' AS "MAXVENTANAHORARIA1",
                ROUND((T2."PriceBefDi"/"Weight1") ,2) AS "COSTOXKILO",
                T2."PriceBefDi" AS "COSTOITEM",
                T2."Weight1" AS "CAPACIDADUNO",
                T4."LicTradNum" AS "IDENTIFICADORCONTACTO",
                T4."CardName" AS "NOMBRECONTACTO",
                T4."Phone1" AS "TELEFONO",
                '' AS "EMAILCONTACTO",
                'Femaco' AS "CTORIGEN",
                T5."SlpName" AS "VENDEDOR",
                T3."U_EXF_ZONRPTS" AS "Name"

                FROM ORDR T1
                INNER JOIN RDR1 T2 ON T1."DocEntry"=T2."DocEntry"
                INNER JOIN RDR12 T3 ON T1."DocEntry"=T3."DocEntry"
                INNER JOIN OCRD T4 ON T1."CardCode"=T4."CardCode"
                INNER JOIN OSLP T5 ON T1."SlpCode"=T5."SlpCode"
                LEFT OUTER JOIN "@EXF_ZONRPT" T6 ON T3."U_EXF_ZONRPTS" = T6."Name"

                WHERE
                T1."DocStatus" = 'O' AND
                T2."Quantity"<>0
                AND T1."U_FEM_ESTIENDA" = 'N'
                AND (T3."U_XM_LatitudS" IS NOT NULL AND TO_DOUBLE(T3."U_XM_LatitudS") != 0 AND TO_DOUBLE(T3."U_XM_LatitudS") BETWEEN -90 AND 90)
                AND (T3."U_XM_LongitudS" IS NOT NULL AND TO_DOUBLE(T3."U_XM_LongitudS") != 0 AND TO_DOUBLE(T3."U_XM_LongitudS") BETWEEN -180 AND 180)
                AND T3."StreetS"<>'FISCAL'
                and T1."DocDate" >=  '
                """
                    + desde +
                    """
                '
                and T1."DocDate" <= '
                """
                    + hasta +
                    """
                '
                and T1."DocDueDate" >= '
                """
                    + desdevenc +
                    """
                '
                and T1."DocDueDate" <= '
                """
                    + hastavenc +
                    """
                '
                and T3."U_XM_TerritorioS" IN (
                """
                    + terri +
                    """
                )
                """;

                    Console.WriteLine(str);
                    IEnumerable<VentaConConsolidado> ventaConConsolidadoSap = hanaConnection.Query<VentaConConsolidado>(str);
                    hanaConnection.Close();
                    var ventaConConsolidado = ventaConConsolidadoSap;
                    Console.WriteLine(str);
                    return Ok(ventaConConsolidado);
                } else {
                    var str = """
                SELECT
                T1."DocNum" AS "NDOCUMENTO",
                T1."DocEntry" AS "ENTRY",
                T2."LineNum" AS "POSICI",
                T3."U_XM_LatitudS" AS "LATITUD",
                T3."U_XM_LongitudS" AS "LONGITUD",
                T3."StreetS" AS "DIRECCION",
                T2."Dscription" AS "NOMBREITEM",
                T2."Quantity" AS "CANTIDAD",
                T2."ItemCode" AS "CODIGOITEM",
                TO_VARCHAR (TO_DATE(T2."ShipDate"), 'DD/MM/YYYY') AS "FECHAMINENTREGA",
                TO_VARCHAR (TO_DATE(T1."DocDueDate"), 'DD/MM/YYYY') AS "FECHAMAXENTREGA",
                '07:30' AS "MINVENTANAHORARIA1",
                '20:30' AS "MAXVENTANAHORARIA1",
                ROUND((T2."PriceBefDi"/"Weight1") ,2) AS "COSTOXKILO",
                T2."PriceBefDi" AS "COSTOITEM",
                T2."Weight1" AS "CAPACIDADUNO",
                T4."LicTradNum" AS "IDENTIFICADORCONTACTO",
                T4."CardName" AS "NOMBRECONTACTO",
                T4."Phone1" AS "TELEFONO",
                '' AS "EMAILCONTACTO",
                'Femaco' AS "CTORIGEN",
                T5."SlpName" AS "VENDEDOR",
                T3."U_EXF_ZONRPTS" AS "Name"

                FROM ORDR T1
                INNER JOIN RDR1 T2 ON T1."DocEntry"=T2."DocEntry"
                INNER JOIN RDR12 T3 ON T1."DocEntry"=T3."DocEntry"
                INNER JOIN OCRD T4 ON T1."CardCode"=T4."CardCode"
                INNER JOIN OSLP T5 ON T1."SlpCode"=T5."SlpCode"
                LEFT OUTER JOIN "@EXF_ZONRPT" T6 ON T3."U_EXF_ZONRPTS" = T6."Name"

                WHERE
                T1."DocStatus" = 'O' AND
                T2."Quantity"<>0
                AND T1."U_FEM_ESTIENDA" = 'N'
                AND (T3."U_XM_LatitudS" IS NOT NULL AND TO_DOUBLE(T3."U_XM_LatitudS") != 0 AND TO_DOUBLE(T3."U_XM_LatitudS") BETWEEN -90 AND 90)
                AND (T3."U_XM_LongitudS" IS NOT NULL AND TO_DOUBLE(T3."U_XM_LongitudS") != 0 AND TO_DOUBLE(T3."U_XM_LongitudS") BETWEEN -180 AND 180)
                AND T3."StreetS"<>'FISCAL'
                and T1."DocDate" >=  '
                """
                    + desde +
                    """
                '
                and T1."DocDate" <= '
                """
                    + hasta +
                    """
                '
                and T1."DocDueDate" >= '
                """
                    + desdevenc +
                    """
                '
                and T1."DocDueDate" <= '
                """
                    + hastavenc +
                    """
                '
                and T5."SlpCode" IN (
                """
                        + rpt +
                    """
                )
                and T3."U_XM_TerritorioS" IN (
                """
                    + terri +
                    """
                )
                """;

                    Console.WriteLine(str);
                    IEnumerable<VentaConConsolidado> ventaConConsolidadoSap = hanaConnection.Query<VentaConConsolidado>(str);
                    hanaConnection.Close();
                    var ventaConConsolidado = ventaConConsolidadoSap;

                    Console.WriteLine(str);
                    return Ok(ventaConConsolidado);
                }
            } else {

                if (length == 0) {
                    var str = """
                SELECT
                T1."DocNum" AS "NDOCUMENTO",
                T1."DocEntry" AS "ENTRY",
                T2."LineNum" AS "POSICI",
                T3."U_XM_LatitudS" AS "LATITUD",
                T3."U_XM_LongitudS" AS "LONGITUD",
                T3."StreetS" AS "DIRECCION",
                T2."Dscription" AS "NOMBREITEM",
                T2."Quantity" AS "CANTIDAD",
                T2."ItemCode" AS "CODIGOITEM",
                TO_VARCHAR (TO_DATE(T2."ShipDate"), 'DD/MM/YYYY') AS "FECHAMINENTREGA",
                TO_VARCHAR (TO_DATE(T1."DocDueDate"), 'DD/MM/YYYY') AS "FECHAMAXENTREGA",
                '07:30' AS "MINVENTANAHORARIA1",
                '20:30' AS "MAXVENTANAHORARIA1",
                ROUND((T2."PriceBefDi"/"Weight1") ,2) AS "COSTOXKILO",
                T2."PriceBefDi" AS "COSTOITEM",
                T2."Weight1" AS "CAPACIDADUNO",
                T4."LicTradNum" AS "IDENTIFICADORCONTACTO",
                T4."CardName" AS "NOMBRECONTACTO",
                T4."Phone1" AS "TELEFONO",
                '' AS "EMAILCONTACTO",
                'Femaco' AS "CTORIGEN",
                T5."SlpName" AS "VENDEDOR",
                T3."U_EXF_ZONRPTS" AS "Name"

                FROM ORDR T1
                INNER JOIN RDR1 T2 ON T1."DocEntry"=T2."DocEntry"
                INNER JOIN RDR12 T3 ON T1."DocEntry"=T3."DocEntry"
                INNER JOIN OCRD T4 ON T1."CardCode"=T4."CardCode"
                INNER JOIN OSLP T5 ON T1."SlpCode"=T5."SlpCode"
                LEFT OUTER JOIN "@EXF_ZONRPT" T6 ON T3."U_EXF_ZONRPTS" = T6."Name"

                WHERE
                T1."DocStatus" = 'O' AND
                T2."Quantity"<>0
                AND T1."U_FEM_ESTIENDA" = 'N'
                AND (T3."U_XM_LatitudS" IS NOT NULL AND TO_DOUBLE(T3."U_XM_LatitudS") != 0 AND TO_DOUBLE(T3."U_XM_LatitudS") BETWEEN -90 AND 90)
                AND (T3."U_XM_LongitudS" IS NOT NULL AND TO_DOUBLE(T3."U_XM_LongitudS") != 0 AND TO_DOUBLE(T3."U_XM_LongitudS") BETWEEN -180 AND 180)
                AND T3."StreetS"<>'FISCAL'
                and T1."DocDate" >=  '
                """
                    + desde +
                    """
                '
                and T1."DocDate" <= '
                """
                    + hasta +
                    """
                '
                and T1."DocDueDate" >= '
                """
                    + desdevenc +
                    """
                '
                and T1."DocDueDate" <= '
                """
                    + hastavenc +
                    """
                '
                and T3."U_EXF_ZONRPTS" IN (
                """
                    + znrpt +
                    """
                )
                and T3."U_XM_TerritorioS" IN (
                """
                    + terri +
                    """
                )
                """;

                    Console.WriteLine(str);
                    IEnumerable<VentaConConsolidado> ventaConConsolidadoSap = hanaConnection.Query<VentaConConsolidado>(str);
                    hanaConnection.Close();
                    var ventaConConsolidado = ventaConConsolidadoSap;
                    Console.WriteLine(str);
                    return Ok(ventaConConsolidado);
                } else {
                    var str = """
                SELECT
                T1."DocNum" AS "NDOCUMENTO",
                T1."DocEntry" AS "ENTRY",
                T2."LineNum" AS "POSICI",
                T3."U_XM_LatitudS" AS "LATITUD",
                T3."U_XM_LongitudS" AS "LONGITUD",
                T3."StreetS" AS "DIRECCION",
                T2."Dscription" AS "NOMBREITEM",
                T2."Quantity" AS "CANTIDAD",
                T2."ItemCode" AS "CODIGOITEM",
                TO_VARCHAR (TO_DATE(T2."ShipDate"), 'DD/MM/YYYY') AS "FECHAMINENTREGA",
                TO_VARCHAR (TO_DATE(T1."DocDueDate"), 'DD/MM/YYYY') AS "FECHAMAXENTREGA",
                '07:30' AS "MINVENTANAHORARIA1",
                '20:30' AS "MAXVENTANAHORARIA1",
                ROUND((T2."PriceBefDi"/"Weight1") ,2) AS "COSTOXKILO",
                T2."PriceBefDi" AS "COSTOITEM",
                T2."Weight1" AS "CAPACIDADUNO",
                T4."LicTradNum" AS "IDENTIFICADORCONTACTO",
                T4."CardName" AS "NOMBRECONTACTO",
                T4."Phone1" AS "TELEFONO",
                '' AS "EMAILCONTACTO",
                'Femaco' AS "CTORIGEN",
                T5."SlpName" AS "VENDEDOR",
                T3."U_EXF_ZONRPTS" AS "Name"

                FROM ORDR T1
                INNER JOIN RDR1 T2 ON T1."DocEntry"=T2."DocEntry"
                INNER JOIN RDR12 T3 ON T1."DocEntry"=T3."DocEntry"
                INNER JOIN OCRD T4 ON T1."CardCode"=T4."CardCode"
                INNER JOIN OSLP T5 ON T1."SlpCode"=T5."SlpCode"
                LEFT OUTER JOIN "@EXF_ZONRPT" T6 ON T3."U_EXF_ZONRPTS" = T6."Name"

                WHERE
                T1."DocStatus" = 'O' AND
                T2."Quantity"<>0
                AND T1."U_FEM_ESTIENDA" = 'N'
                AND (T3."U_XM_LatitudS" IS NOT NULL AND TO_DOUBLE(T3."U_XM_LatitudS") != 0 AND TO_DOUBLE(T3."U_XM_LatitudS") BETWEEN -90 AND 90)
                AND (T3."U_XM_LongitudS" IS NOT NULL AND TO_DOUBLE(T3."U_XM_LongitudS") != 0 AND TO_DOUBLE(T3."U_XM_LongitudS") BETWEEN -180 AND 180)
                AND T3."StreetS"<>'FISCAL'
                and T1."DocDate" >=  '
                """
                    + desde +
                    """
                '
                and T1."DocDate" <= '
                """
                    + hasta +
                    """
                '
                and T1."DocDueDate" >= '
                """
                    + desdevenc +
                    """
                '
                and T1."DocDueDate" <= '
                """
                    + hastavenc +
                    """
                '
                and T3."U_EXF_ZONRPTS" IN (
                """
                    + znrpt +
                    """
                )
                and T5."SlpCode" IN (
                """
                        + rpt +
                    """
                )
                and T3."U_XM_TerritorioS" IN (
                """
                    + terri +
                    """
                )
                """;

                    Console.WriteLine(str);
                    IEnumerable<VentaConConsolidado> ventaConConsolidadoSap = hanaConnection.Query<VentaConConsolidado>(str);
                    hanaConnection.Close();
                    var ventaConConsolidado = ventaConConsolidadoSap;

                    Console.WriteLine(str);
                    return Ok(ventaConConsolidado);
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
            Console.WriteLine("The length of the string is: " + length);
            var hanaConnection = new HanaConnection("server=192.168.1.9:30015;CurrentSchema=FEMACO_PROD;userid=SAPINST;password=F3m4c0H4n4");
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
                AND (T2."U_XM_LatitudS" IS NOT NULL AND TO_DOUBLE(T2."U_XM_LatitudS") != 0 AND TO_DOUBLE(T2."U_XM_LatitudS") BETWEEN -90 AND 90)
                AND (T2."U_XM_LongitudS" IS NOT NULL AND TO_DOUBLE(T2."U_XM_LongitudS") != 0 AND TO_DOUBLE(T2."U_XM_LongitudS") BETWEEN -180 AND 180)
                AND T2."StreetS"<>'FISCAL'
                AND T1."DocDate" >=  '
                """
                        + desde +
                    """
                '
                AND T1."DocDate" <= '
                """
                        + hasta +
                    """
                '
                AND T1."DocDueDate" >= '
                """
                    + desdevenc +
                    """
                '
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
                AND (T2."U_XM_LatitudS" IS NOT NULL AND TO_DOUBLE(T2."U_XM_LatitudS") != 0 AND TO_DOUBLE(T2."U_XM_LatitudS") BETWEEN -90 AND 90)
                AND (T2."U_XM_LongitudS" IS NOT NULL AND TO_DOUBLE(T2."U_XM_LongitudS") != 0 AND TO_DOUBLE(T2."U_XM_LongitudS") BETWEEN -180 AND 180)
                AND T2."StreetS"<>'FISCAL'
                and T1."DocDate" >=  '
                """
                        + desde +
                        """
                '
                and T1."DocDate" <= '
                """
                        + hasta +
                        """
                '
                and T1."DocDueDate" >= '
                """
                    + desdevenc +
                    """
                '
                and T1."DocDueDate" <= '
                """
                    + hastavenc +
                    """
                '
                and T1."SlpCode" IN (
                """
                        + rpt +
                    """
                )
                and T2."U_XM_TerritorioS" IN (
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
                AND (T2."U_XM_LatitudS" IS NOT NULL AND TO_DOUBLE(T2."U_XM_LatitudS") != 0 AND TO_DOUBLE(T2."U_XM_LatitudS") BETWEEN -90 AND 90)
                AND (T2."U_XM_LongitudS" IS NOT NULL AND TO_DOUBLE(T2."U_XM_LongitudS") != 0 AND TO_DOUBLE(T2."U_XM_LongitudS") BETWEEN -180 AND 180)
                AND T2."StreetS"<>'FISCAL'
                and T1."DocDate" >=  '
                """
                        + desde +
                    """
                '
                and T1."DocDate" <= '
                """
                        + hasta +
                    """
                '
                and T1."DocDueDate" >= '
                """
                    + desdevenc +
                    """
                '
                and T1."DocDueDate" <= '
                """
                    + hastavenc +
                    """
                ' 
                and T2."U_EXF_ZONRPTS" IN (
                """
                + znrpt +
                """
                )
                and T2."U_XM_TerritorioS" IN (
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
                AND (T2."U_XM_LatitudS" IS NOT NULL AND TO_DOUBLE(T2."U_XM_LatitudS") != 0 AND TO_DOUBLE(T2."U_XM_LatitudS") BETWEEN -90 AND 90)
                AND (T2."U_XM_LongitudS" IS NOT NULL AND TO_DOUBLE(T2."U_XM_LongitudS") != 0 AND TO_DOUBLE(T2."U_XM_LongitudS") BETWEEN -180 AND 180)
                AND T2."StreetS"<>'FISCAL'
                and T1."DocDate" >=  '
                """
                        + desde +
                        """
                '
                and T1."DocDate" <= '
                """
                        + hasta +
                        """
                '
                and T1."DocDueDate" >= '
                """
                    + desdevenc +
                    """
                '
                and T1."DocDueDate" <= '
                """
                    + hastavenc +
                    """
                '
                and T2."U_EXF_ZONRPTS" IN (
                """
                + znrpt +
                """
                )
                and T1."SlpCode" IN (
                """
                        + rpt +
                    """
                )
                and T2."U_XM_TerritorioS" IN (
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
            var hanaConnection = new HanaConnection("server=192.168.1.9:30015;CurrentSchema=FEMACO_PROD;userid=SAPINST;password=F3m4c0H4n4");
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
                    WHEN T3."U_XM_LatitudS" IS NULL OR T3."U_XM_LongitudS" IS NULL OR T1."Address2" = 'FISCAL' OR T3."U_XM_TerritorioS" IS NULL OR TO_DOUBLE(T3."U_XM_LatitudS") = 0 OR TO_DOUBLE(T3."U_XM_LongitudS") = 0 OR TO_DOUBLE(T3."U_XM_LatitudS") < -90 OR TO_DOUBLE(T3."U_XM_LatitudS") > 90 OR TO_DOUBLE(T3."U_XM_LongitudS") < -180 OR TO_DOUBLE(T3."U_XM_LongitudS") > 180 OR TO_DOUBLE(T2."Weight1") = 0 OR T2."Weight1" IS NULL THEN 1
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
                and T1."DocDate" >=  '
                """
                        + desde +
                    """
                '
                and T1."DocDate" <= '
                """
                        + hasta +
                    """
                '
                and T1."DocDueDate" >= '
                """
                    + desdevenc +
                    """
                '
                and T1."DocDueDate" <= '
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
                    WHEN T3."U_XM_LatitudS" IS NULL OR T3."U_XM_LongitudS" IS NULL OR T1."Address2" = 'FISCAL' OR T3."U_XM_TerritorioS" IS NULL OR TO_DOUBLE(T3."U_XM_LatitudS") = 0 OR TO_DOUBLE(T3."U_XM_LongitudS") = 0 OR TO_DOUBLE(T3."U_XM_LatitudS") < -90 OR TO_DOUBLE(T3."U_XM_LatitudS") > 90 OR TO_DOUBLE(T3."U_XM_LongitudS") < -180 OR TO_DOUBLE(T3."U_XM_LongitudS") > 180 OR TO_DOUBLE(T2."Weight1") = 0 OR T2."Weight1" IS NULL THEN 1
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
                and T1."DocDate" >=  '
                """
                        + desde +
                        """
                '
                and T1."DocDate" <= '
                """
                        + hasta +
                        """
                '
                and T1."DocDueDate" >= '
                """
                    + desdevenc +
                    """
                '
                and T1."DocDueDate" <= '
                """
                    + hastavenc +
                    """
                '
                and T5."SlpCode" IN (
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
                    WHEN T3."U_XM_LatitudS" IS NULL OR T3."U_XM_LongitudS" IS NULL OR T1."Address2" = 'FISCAL' OR T3."U_XM_TerritorioS" IS NULL OR TO_DOUBLE(T3."U_XM_LatitudS") = 0 OR TO_DOUBLE(T3."U_XM_LongitudS") = 0 OR TO_DOUBLE(T3."U_XM_LatitudS") < -90 OR TO_DOUBLE(T3."U_XM_LatitudS") > 90 OR TO_DOUBLE(T3."U_XM_LongitudS") < -180 OR TO_DOUBLE(T3."U_XM_LongitudS") > 180 OR TO_DOUBLE(T2."Weight1") = 0 OR T2."Weight1" IS NULL THEN 1
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
                and T1."DocDate" >=  '
                """
                        + desde +
                    """
                '
                and T1."DocDate" <= '
                """
                        + hasta +
                    """
                '
                and T1."DocDueDate" >= '
                """
                    + desdevenc +
                    """
                '
                and T1."DocDueDate" <= '
                """
                    + hastavenc +
                    """
                '
                and T3."U_EXF_ZONRPTS" IN (
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
                    WHEN T3."U_XM_LatitudS" IS NULL OR T3."U_XM_LongitudS" IS NULL OR T1."Address2" = 'FISCAL' OR T3."U_XM_TerritorioS" IS NULL OR TO_DOUBLE(T3."U_XM_LatitudS") = 0 OR TO_DOUBLE(T3."U_XM_LongitudS") = 0 OR TO_DOUBLE(T3."U_XM_LatitudS") < -90 OR TO_DOUBLE(T3."U_XM_LatitudS") > 90 OR TO_DOUBLE(T3."U_XM_LongitudS") < -180 OR TO_DOUBLE(T3."U_XM_LongitudS") > 180 OR TO_DOUBLE(T2."Weight1") = 0 OR T2."Weight1" IS NULL THEN 1
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
                and T1."DocDate" >=  '
                """
                        + desde +
                        """
                '
                and T1."DocDate" <= '
                """
                        + hasta +
                        """
                '
                and T1."DocDueDate" >= '
                """
                    + desdevenc +
                    """
                '
                and T1."DocDueDate" <= '
                """
                    + hastavenc +
                    """
                '
                and T3."U_EXF_ZONRPTS" IN (
                """
                + znrpt +
                """
                )
                and T5."SlpCode" IN (
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
            var hanaConnection = new HanaConnection("server=192.168.1.9:30015;CurrentSchema=FEMACO_PROD;userid=SAPINST;password=F3m4c0H4n4");
            try {
                hanaConnection.Open();
                foreach (dynamic i in jsonObject.numerosGuia) {
                    // PUEDE DARSE EL CASO DE QUE UNA ORDEN DE VETNA VAYA A DOS PICKING PERO SOLO ANALIZO EL PEDIDO EN GENERAL ESTE METODO
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
            var hanaConnection = new HanaConnection("server=192.168.1.9:30015;CurrentSchema=FEMACO_PROD;userid=SAPINST;password=F3m4c0H4n4");
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
        public IActionResult MostrarDatos([FromForm] IFormFile ArchivoExcel) {
            Stream stream = ArchivoExcel.OpenReadStream();
            IWorkbook MiExcel = null;
            if (Path.GetExtension(ArchivoExcel.FileName) == ".xlsx") {
                MiExcel = new XSSFWorkbook(stream);
            } else {
                MiExcel = new HSSFWorkbook(stream);
            }
            string vehiculo = null;
            string capacidad = null;
            string utilizado = null;

            List<VMContacto> listaTotal = new List<VMContacto>();

            for (int hojaIndex = 0; hojaIndex < MiExcel.NumberOfSheets; hojaIndex++) {
                ISheet hoja = MiExcel.GetSheetAt(hojaIndex);

                IRow filaPlaca = hoja.GetRow(20);
                ICell celdaPlaca = filaPlaca.GetCell(1);
                vehiculo = celdaPlaca?.ToString();

                IRow filaCapacidad = hoja.GetRow(13);
                ICell celdaCapacidad = filaCapacidad.GetCell(1);
                capacidad = celdaCapacidad?.ToString();

                IRow filaUtilizado = hoja.GetRow(15);
                ICell celdaUtilizado = filaUtilizado.GetCell(1);
                utilizado = celdaUtilizado?.ToString();

                int cantidadFilas = hoja.LastRowNum;

                List<VMContacto> lista = new List<VMContacto>();

                for (int i = 29; i <= cantidadFilas; i++) {
                    IRow fila = hoja.GetRow(i);

                    lista.Add(new VMContacto {
                        vehiculo = vehiculo,
                        capacidad = capacidad,
                        utilizado = utilizado,
                        numeroguia = fila.GetCell(0)?.ToString(),
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
            return StatusCode(StatusCodes.Status200OK, listaTotal);

        }
        public async Task<IActionResult> SubirInfo() {

            string usuario = "jdiaz";
            string clave = "Abc123";
            string numero = "";
            Company oCompany = ConexionSap(usuario, clave);

            try {
                string jsonString = @"
                {
                    ""ventas"": [
                        {
                            ""id"": 847777,
                        }
                    ]
                }";

                JObject jsonData = JObject.Parse(jsonString);
                var ventas = jsonData["ventas"].ToObject<VentaConConsolidado[]>();

                if (string.IsNullOrEmpty(usuario) || string.IsNullOrEmpty(clave)) {
                    throw new Exception("No se pudo recuperar el usuario y la clave.");
                }

                if (!oCompany.Connected) {
                    int result = oCompany.Connect();
                    if (result != 0) {
                        throw new Exception($"Error al conectar a SAP B1. Código de error: {result}");
                    }
                }
                Documents oDocuments = (Documents)oCompany.GetBusinessObject(BoObjectTypes.oOrders);

                Document_Lines oDocument_Lines = oDocuments.Lines;
                PickLists oPickLists = (PickLists)oCompany.GetBusinessObject(BoObjectTypes.oPickLists);
                oPickLists.UserFields.Fields.Item("U_EXF_PLC").Value = "VAF895";
                PickLists_Lines oPickLists_Lines = oPickLists.Lines;
                oPickLists.PickDate = DateTime.Today;
                oPickLists_Lines.BaseObjectType = "17";
                oPickLists_Lines.OrderEntry = 3857;
                oPickLists_Lines.OrderRowID = 0;
                oPickLists_Lines.ReleasedQuantity = 1;
                oPickLists_Lines.Add();

                int RetVal = oPickLists.Add();
                if (oPickLists.Add() == 0) {
                    oCompany.EndTransaction(BoWfTransOpt.wf_Commit);
                    numero = oCompany.GetNewObjectKey();
                } else {
                    numero += " " + oCompany.GetLastErrorDescription();
                    oCompany.EndTransaction(BoWfTransOpt.wf_RollBack);
                }

                return Ok("La información se ha subido correctamente a SAP B1.");
            } catch (Exception ex) {
                return StatusCode(500, $"Se produjo un error al subir la información a SAP PRUEBAS: {ex.Message}" + usuario + numero);
            } finally {
                if (oCompany.Connected) {
                    oCompany.Disconnect();
                }
            }
        }
        private Company ConexionSap(string usuario, string clave) {
            Company mycompany = new Company();
            mycompany.DbServerType = SAPbobsCOM.BoDataServerTypes.dst_HANADB;
            mycompany.Server = "NDB@192.168.1.9:30013";
            mycompany.CompanyDB = "ZSBO_PRUEBAS_FEX";
            mycompany.UserName = usuario;
            mycompany.Password = clave;
            mycompany.language = SAPbobsCOM.BoSuppLangs.ln_Spanish_La;
            error = mycompany.Connect();
            return mycompany;
        }
        public IActionResult DeleteReview(int[] id) {
            using System.Data.IDbConnection con = _context.CreateConnection();
            return Json($"Se Eliminaron {id.Length} registros");
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