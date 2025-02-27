using Dapper;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Data.SqlClient;
using System.Text.Json;
using System.Net.Http;
using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using System.Text;
using Sap.Data.Hana;

namespace MorosidadWeb.Controllers {
    public class UserFemacoController : Controller {

        private readonly HttpClient _httpClient;

        private readonly string _hanaConnectionString;
        private readonly string _connectionString;
        public UserFemacoController(IConfiguration configuration) {
            _httpClient = new HttpClient {
                BaseAddress = new Uri("http://192.168.1.9:50001/b1s/v1/")
            };
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            _hanaConnectionString = configuration.GetConnectionString("FemacoSap");
            _connectionString = configuration.GetConnectionString("Femaco10");
        }

        public ActionResult Index() => View("Login");

        public ActionResult Login() => View();

        [HttpPost]
        public async Task<ActionResult> Login(string usuario, string clave, string returnUrl, bool recordarme = false) {
            Console.WriteLine("Usuario: " + usuario);

            if (string.IsNullOrWhiteSpace(usuario) || string.IsNullOrWhiteSpace(clave)) {
                ModelState.AddModelError(string.Empty, "Debe ingresar usuario y clave");
                return View();
            }

            if (int.TryParse(usuario, out _)) {
                var claveEsperada = usuario.Substring(usuario.Length - 4);

                if (clave != claveEsperada) {
                    ModelState.AddModelError(string.Empty, "Clave incorrecta");
                    return View();
                }

                var (nombreUsuario, idpp, puesto) = await ObtenerNombreUsuario(usuario);
                if (string.IsNullOrWhiteSpace(nombreUsuario)) {
                    ModelState.AddModelError(string.Empty, "Usuario no existe");
                    return View();
                }

                var puestoValue = int.TryParse(usuario, out _) ? puesto : "1";
                await IniciarSesion(nombreUsuario, recordarme, puestoValue, clave, idpp, 0);
                if (puesto == "1") {
                    return RedirectToAction("Index", "Home");
                } else if (puesto == "3" || puesto=="2") {
                    return RedirectToAction("PickeadorConteo", "Picking", new { idpp });
                } else if (puesto == "2" || puesto =="3") {
                    return RedirectToAction("JefeConteo", "Picking", new { idpp });
                }
                ModelState.AddModelError(string.Empty, "Puesto no válido");
                return View();
            } else {
                Console.WriteLine("Clave: " + clave);
                try {
                    var token = await GetAuthToken(usuario, clave);
                    if (token == null) {
                        ModelState.AddModelError(string.Empty, "Usuario o clave incorrecto");
                        return View();
                    }
                } catch (Exception ex) {
                    Console.WriteLine("Error al obtener el token de autenticación: " + ex.Message);
                    Console.WriteLine(ex.StackTrace);
                    return View();
                }

                await IniciarSesion(usuario, recordarme, "1", clave, 0, 1);
                return LocalRedirect(returnUrl ?? "/");
            }
        }

        private async Task IniciarSesion(string usuario, bool recordarme, string puesto, string clave, int idpp, int validar) {

            string almacen = null;
            if(validar == 1) {
                HanaConnection hanaConnection = new(_hanaConnectionString);

                await hanaConnection.OpenAsync();
                string sappQuery = $@"
                    SELECT ""Warehouse"" FROM OUDG WHERE ""Code"" = '{usuario}'";
                using (var hanaCommand = new HanaCommand(sappQuery, hanaConnection)) {
                    using (var reader = await hanaCommand.ExecuteReaderAsync()) {
                        if (await reader.ReadAsync()) {
                            almacen = reader["Warehouse"].ToString();
                            Console.WriteLine("Este es el almacén: " + almacen);
                        }
                    }
                }
                hanaConnection.Close();
            } else {
                almacen = "Ninguno";
            }

            ICollection<Claim> claims = new List<Claim> {
                new Claim(ClaimTypes.NameIdentifier, usuario),
                new Claim(ClaimTypes.UserData, JsonSerializer.Serialize(new { usuario, clave, puesto, idpp })),
                new Claim("Puesto", puesto),
                new Claim("IDPP", idpp.ToString()),
                new Claim("Almacen", almacen.ToString())
            };

            ClaimsIdentity identity = new(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            ClaimsPrincipal principal = new(identity);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, new AuthenticationProperties { IsPersistent = recordarme });
        }

        public async Task<IActionResult> LogOut() {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return LocalRedirect("/Home");
        }

        private async Task<(string Nombre, int IDPP, string Puesto)> ObtenerNombreUsuario(string dni) {
            using (var connection = new SqlConnection(_connectionString)) {
                var query = "SELECT Nombre, IDPP, Puesto FROM PickeoPersonal WHERE DNI = @Dni";
                var result = await connection.QueryFirstOrDefaultAsync<(string Nombre, int IDPP, string Puesto)>(query, new { Dni = dni });
                return result;
            }
        }

        private async Task<string> GetAuthToken(string usuario, string clave) {
            var loginData = new {
                CompanyDB = "FEMACO_PROD",
                UserName = usuario,
                Password = clave
            };

            var jsonLoginData = JsonSerializer.Serialize(loginData);
            var content = new StringContent(jsonLoginData, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("Login", content);

            if (response.IsSuccessStatusCode) {
                var jsonResponse = await response.Content.ReadAsStringAsync();
                var token = JsonSerializer.Deserialize<AuthToken>(jsonResponse);
                return token.SessionId;
            }

            return null;
        }

        private class AuthToken {
            public string SessionId { get; set; }
        }
    }
}