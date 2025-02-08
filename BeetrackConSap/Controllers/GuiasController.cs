using Microsoft.AspNetCore.Mvc;
using System.Data.SqlClient;
using Sap.Data.Hana;
using Dapper;
using BeetrackConSap.Models;
using Microsoft.AspNetCore.Identity.Data;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using System.IO;

namespace BeetrackConSap.Controllers {
    public class GuiasController : Controller {

        private readonly string _hanaConnectionString;
        private readonly string _connectionString;

        public GuiasController(IConfiguration configuration) {
            _connectionString = configuration.GetConnectionString("Femaco10");
            _hanaConnectionString = configuration.GetConnectionString("FemacoSap");
        }
        public IActionResult Index() {
            return View("~/Views/Cargada/CargadaVista.cshtml");
        }
        public IActionResult GuiasPlanificacion() {
            return View("~/Views/Guias/GuiasPlanificacion.cshtml");
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
                    COALESCE(T1.Cargado,0) AS Cargado
                    FROM Planificacion T0
                    INNER JOIN PlanificacionPlaca T1 ON T0.IDPlan = T1.IDPlan
                    LEFT JOIN PickeoProducto T2 ON T2.IDPlaca = T1.IDPlanPla
                    LEFT JOIN PickeoProducto T3 ON T3.IDPProducto = T2.IDPProducto AND T3.Finalizado = 1
                    GROUP BY T1.Placa, T1.IDPlanPla, T1.Cargado";

                var result = await connection.QueryAsync<CargaPlan>(query);
                return Ok(result);
            }
        }

        [HttpGet]
        public IActionResult GenerarGuias(int id) {
            var placa = "Datos fijos"; 

            using (var ms = new MemoryStream()) {
                using (PdfWriter writer = new PdfWriter(ms)) {
                    using (PdfDocument pdf = new PdfDocument(writer)) {
                        Document document = new Document(pdf);
                        document.Add(new Paragraph("Guía Generada"));
                        document.Add(new Paragraph($"Placa: {placa}"));
                    }
                }

                return File(ms.ToArray(), "application/pdf", "Guia.pdf");
            }
        }

        [HttpPost]
        public async Task<IActionResult> FinalizarCargaPlaca(int idPlan) {
            using (var connection = new SqlConnection(_connectionString)) {
                await connection.OpenAsync();
                string query = @"
                    UPDATE PlanificacionPlaca SET Cargado = 1 WHERE IDPlanPla = @idPlan";
                var parameters = new { idPlan };

                var result = await connection.ExecuteAsync(query, parameters);

                if (result > 0) {
                    return Ok(new { success = true, message = "Carga finalizado exitosamente." });
                } else {
                    return NotFound(new { success = false, message = "No se encontraron registros para actualizar." });
                }
            }
        }

    }
}
