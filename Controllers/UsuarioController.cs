using Delivery.Domain.User;
using Delivery.Repositories.Interfaces;
using Microsoft.AspNetCore.Mvc;

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Delivery.Domain.Food;
using System.Drawing;
using System;
using System.Runtime.CompilerServices;

namespace Delivery.Controllers
{
    public class UsuarioController : Controller
    {
        private readonly IUsuarioRepository _usuarioRepository;
        public UsuarioController(IUsuarioRepository usuarioRepository)
        {
            _usuarioRepository = usuarioRepository;
        }

        [Authorize(Roles = "Administrador")]
        public async Task<IActionResult> TablaUsuarios(string tipo = "Cliente")
        {
            Rol rol = (Rol)Enum.Parse(typeof(Rol), tipo);

            if(rol == Rol.Administrador)
            {
                ViewBag.usuarios = await _usuarioRepository.ObtenerTodos(u => u.Rol == rol && u.Id != int.Parse(User.FindFirst("ID").Value));
            }
            else ViewBag.usuarios = await _usuarioRepository.ObtenerTodos(u => u.Rol == rol);
            ViewBag.tipo = tipo;

            return View();
        }

        [Authorize(Roles = "Administrador")]
        public IActionResult RegisterEmpleados(string tipo = "repartidor")
        {
            ViewBag.tipo_e = tipo;
            return View();
        }

        [HttpPost]
        [Authorize(Roles = "Administrador")]
        public async Task<IActionResult> RegisterEmpleados(
             [Bind("Surname, Name, Phone, Sexo, Email, Password, DateBirth")] Usuario usuario, string tipo)
        {
            bool modelovalido = true;

            if (await _usuarioRepository.EmailRepetido(usuario.Email))
            {
                modelovalido = false;
                ViewBag.EmailRepetido = "El correo ingresado ya existe";
            }
            if (await _usuarioRepository.PhoneRepetido(usuario.Phone))
            {
                modelovalido = false;
                ViewBag.PhoneRepetido = "El telefono ingresado ya fue registrado";
            }

            if (ModelState.IsValid && modelovalido)
            {
                usuario.Rol = tipo switch
                {
                    "Repartidor" => Rol.Repartidor,
                    "Chef" => Rol.Chef,
                    "Administrador" => Rol.Administrador,
                    _ => Rol.Repartidor,
                };
                usuario.Password = _usuarioRepository.EncriptarSHA256(usuario.Password); //Encriptar contraseña

                await _usuarioRepository.RegistrarUsuario(usuario);
                return RedirectToAction("TablaUsuarios", "Usuario");

            }


            ViewBag.SurnameError = ModelState["Surname"].Errors.Count > 0;
            ViewBag.NameError = ModelState["Name"].Errors.Count > 0;
            ViewBag.PhoneError = ModelState["Phone"].Errors.Count > 0;
            ViewBag.EmailError = ModelState["Email"].Errors.Count > 0;
            ViewBag.PasswordError = ModelState["Password"].Errors.Count > 0;
            ViewBag.DataBirthError = ModelState["DateBirth"].Errors.Count > 0;

            /*
            foreach (var estado in ModelState.Values)
            {
                foreach (var error in estado.Errors)
                {
                    Console.WriteLine($"Campo: {estado} - Error: {error.ErrorMessage}");
                }
            }
            */

            return View();
        }


        [Authorize(Roles = "Administrador")]
        public async Task<IActionResult> Bloquear_Usuario(int UID)
        {
            string retorno = await _usuarioRepository.Bloquear_Usuario(UID);
            return Json(retorno);
        }

        [Authorize(Roles = "Administrador")]
        public async Task<IActionResult> Desbloquear_Usuario(int UID)
        {
            string retorno = await _usuarioRepository.Desbloquear_Usuario(UID);
            return Json(retorno);
        }

        [Authorize(Roles = "Administrador")]
        public async Task<IActionResult> Cambiar_Rol(int UID, string rol)
        {
            Rol aux = (Rol)Enum.Parse(typeof(Rol), rol);
            await _usuarioRepository.Cambiar_Rol(UID, aux);
            return Json(null);
        }

        [Authorize]
        public async Task<IActionResult> EditarDatos()
        {
            int idUser = int.Parse(User.FindFirstValue("ID"));
            Usuario usuario = await _usuarioRepository.BuscarUsuario(idUser);
            return View(usuario);
        }

        [HttpPost]
        public async Task<IActionResult> EditarDatos(
            [Bind("Id, Surname, Name, Phone, Email, Sexo, DateBirth, Password")] Usuario usuario)
        {
            var update_Usuario = await _usuarioRepository.BuscarUsuario(usuario.Id);
            update_Usuario.Surname = usuario.Surname;
            update_Usuario.Name = usuario.Name;
            update_Usuario.Phone = usuario.Phone;
            update_Usuario.Sexo = usuario.Sexo;
            update_Usuario.DateBirth = usuario.DateBirth;

            if(usuario.Password is not null)
                update_Usuario.Password = _usuarioRepository.EncriptarSHA256(usuario.Password);


            _usuarioRepository.Actualizar(update_Usuario);
            await _usuarioRepository.Guardar();
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login", "Usuario");
        }


        [AllowAnonymous]
        public IActionResult Login()
        {
            if (User.Identity.IsAuthenticated) return RedirectToAction("Index", "Home");
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(Usuario usuario)
        {
            string contraseña = _usuarioRepository.EncriptarSHA256(usuario.Password);
            var _usuario = await _usuarioRepository.ValidarUsuario(usuario.Email, contraseña);

            if(_usuario != null)
            {

                if (_usuario.Bloqueado)
                {
                    ViewBag.UserNoValid = "Tu cuenta ha sido bloqueada, consulte a un administrador del delivery para más detalles";
                    return View();
                }

                var claims = new List<Claim> {
                    new Claim("Nombre", _usuario.Name),
                    new Claim("Correo", _usuario.Email),
                    new Claim("ID", _usuario.Id.ToString()),
                    new Claim("Apellido", _usuario.Surname),
                    new Claim("Telefono", _usuario.Phone)

                };

                claims.Add(new Claim(ClaimTypes.Role, _usuario.Rol.ToString()));

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity));

                return RedirectToAction("Index", "Home");
            }
            else
            {
                ViewBag.UserNoValid = "El correo y la contraseña no coinciden, intentelo nuevamente";
                return View();
            }
        }

        [AllowAnonymous]
        public IActionResult Register()
        {
            if (User.Identity.IsAuthenticated) return RedirectToAction("Index", "Home");
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Register(
            [Bind("Surname, Name, Phone, Sexo, Email, Password, DateBirth")] Usuario cliente)
        {
            bool modelovalido = true;

            if (await _usuarioRepository.EmailRepetido(cliente.Email))
            {
                modelovalido = false;
                ViewBag.EmailRepetido = "El correo ingresado ya existe";
            }
            if (await _usuarioRepository.PhoneRepetido(cliente.Phone))
            {
                modelovalido = false;
                ViewBag.PhoneRepetido = "El telefono ingresado ya fue registrado";
            }

            if (ModelState.IsValid && modelovalido)
            {
                cliente.Rol = Rol.Cliente;
                cliente.Password = _usuarioRepository.EncriptarSHA256(cliente.Password); //Encriptar contraseña
                await _usuarioRepository.RegistrarUsuario(cliente);
                return RedirectToAction("Login");

            }


            ViewBag.SurnameError = ModelState["Surname"].Errors.Count > 0;
            ViewBag.NameError = ModelState["Name"].Errors.Count > 0;
            ViewBag.PhoneError = ModelState["Phone"].Errors.Count > 0;
            ViewBag.EmailError = ModelState["Email"].Errors.Count > 0;
            ViewBag.PasswordError = ModelState["Password"].Errors.Count > 0;
            ViewBag.DataBirthError = ModelState["DateBirth"].Errors.Count > 0;

            return View();
        }



        public async Task<IActionResult> Salir()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login", "Usuario");
        }
    }
}
