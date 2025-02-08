using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ExcelDataReader;
using OfficeOpenXml;
using MorosidadWeb.Models;

namespace MorosidadWeb.Controllers {
    public class ExcelController : Controller {
        private readonly IWebHostEnvironment _hostingEnvironment;

        public ExcelController(IWebHostEnvironment hostingEnvironment) {
            _hostingEnvironment = hostingEnvironment;
        }

        public IActionResult SubirArchivoExcel() {
            return View();
        }

        [HttpPost]
        public IActionResult Upload(IFormFile file) {
            try {
                if (file != null && file.Length > 0) {
                    string uploadsFolder = Path.Combine(_hostingEnvironment.WebRootPath, "uploads");
                    string uniqueFileName = Guid.NewGuid().ToString() + "_" + file.FileName;
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var stream = new FileStream(filePath, FileMode.Create)) {
                        file.CopyTo(stream);
                    }

                    using (var package = new ExcelPackage(new FileInfo(filePath))) {
                        ExcelWorksheet worksheet = package.Workbook.Worksheets[0];
                        int rowCount = worksheet.Dimension.Rows;
                        int colCount = worksheet.Dimension.Columns;

                        List<List<string>> data = new List<List<string>>();

                        for (int row = 1; row <= rowCount; row++) {
                            List<string> rowList = new List<string>();
                            for (int col = 1; col <= colCount; col++) {
                                rowList.Add(worksheet.Cells[row, col].Value?.ToString() ?? "");
                            }
                            data.Add(rowList);
                        }

                        ViewBag.Message = "Archivo Excel cargado correctamente.";
                        return PartialView("TablaExcel", data);
                    }
                } else {
                    ViewBag.Message = "No se ha seleccionado ningún archivo o el archivo está vacío.";
                    return PartialView("Error", new ErrorViewModel { Message = ViewBag.Message });
                }
            } catch (Exception ex) {
                ViewBag.Message = "Ocurrió un error al procesar el archivo Excel: " + ex.Message;
                return PartialView("Error", new ErrorViewModel { Message = ViewBag.Message });
            }
        }
    }
}