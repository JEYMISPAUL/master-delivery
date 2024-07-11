using Delivery.Domain.Food;
using Delivery.Domain.User;
using Delivery.Persistence.Data;
using Delivery.Repositories.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Delivery.Controllers
{
    public class ComidaController : Controller
    {
        private readonly IComidaRepository _comidaRepository;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly ICompositeViewEngine _viewEngine;
        private readonly DeliveryDBContext _context;
        public ComidaController(
            IComidaRepository comidaRepository,
            IWebHostEnvironment webHostEnvironment,
            ICompositeViewEngine viewEngine,
            DeliveryDBContext context)
        {
            _comidaRepository = comidaRepository;
            _webHostEnvironment = webHostEnvironment;
            _viewEngine = viewEngine;
            _context = context; 
        }

        #region VerMenu

        public async Task<IActionResult> ConsultarStock(int idComida)
        {
            var comida = await _comidaRepository.ObtenerPorId(idComida);
            int stock = comida.Stock;
            return Json(stock);
        }

        public async Task<IActionResult> RealizarPedido(string listaComidasPedido, string idcliente, string precio)
        {
            TempData["lista"] = listaComidasPedido;
            TempData["PrecioTotal"] = precio;
            return RedirectToAction("CrearPedido", "Pedido");
        }

        //Vista parcial para ver las comidas pedidas antes de hacer el envio
        public async Task<IActionResult> _VerComidasPedido()
        {   
            return PartialView();
        }

        //Vista parcial para elegir alguna característica de una comida
        [Authorize(Roles = "Cliente")]
        public async Task<IActionResult> _ElegirCaracComida(int id = 0)
        {
            await _comidaRepository.ObtenerCaracteristicasComidas();
            if (id is 0) return PartialView();
            else
            {
                Comida comida = await _comidaRepository.ObtenerPorId(id);
                ViewBag.caracteristicas2 = await _comidaRepository.ObtenerCaracteristicasPorComidaID(id);
                return PartialView(comida);
            }
        }


        //Función auxiliar para convertir un PartialView a String
        private async Task<string> RenderPartialViewToString(string viewName, object model)
        {
            if (string.IsNullOrEmpty(viewName))
                viewName = ControllerContext.ActionDescriptor.ActionName;

            ViewData.Model = model;

            using (var writer = new StringWriter())
            {
                ViewEngineResult viewResult =
                    _viewEngine.FindView(ControllerContext, viewName, false);

                ViewContext viewContext = new ViewContext(
                    ControllerContext,
                    viewResult.View,
                    ViewData,
                    TempData,
                    writer,
                    new HtmlHelperOptions()
                );

                await viewResult.View.RenderAsync(viewContext);

                return writer.GetStringBuilder().ToString();
            }
        }


        //Función que devuelve la lista de comidas filtradas
        public async Task<IActionResult> _ListaComidas(string busqueda = "", int? CategoriaComida = null, int page = 1)
        {
            IEnumerable<Comida> lista = Enumerable.Empty<Comida>();

            //Lista según rol
            if (User.IsInRole("Cliente"))
                lista = await _comidaRepository.ObtenerTodos(c => c.MenuDelDia);
            else
                lista = await _comidaRepository.ObtenerTodos();

            //Busqueda por nombre
            if (busqueda.Trim() != string.Empty)
                lista = lista.Where(c => c.Nombre.ToLower().Contains(busqueda.ToLower()));

            //Filtrado categoría
            if (CategoriaComida != null)
                lista = lista.Where(c => ((int)c.Categoria) == CategoriaComida);

            var paginas_Totales = _comidaRepository.PaginasTotales(lista);

            //Paginación
            lista = _comidaRepository.Obtener_comidas_paginado(lista, page);

            //Retonar lista como HTML
            string comidas_HTML = await RenderPartialViewToString("_ListaComidas", lista);
            
            //Pasar multiples objetos
            var retorno = new
            {
                html = comidas_HTML,
                paginas_totales = paginas_Totales

            };

            return Json(retorno);
        }

        [HttpGet]
        public async Task<IActionResult> VerMenu()
        {
            return View("VerMenu");
        }

        #endregion

        #region Editar Menú

        //Vista de modificar el menú en general
        //TODO: Agregar restricciones de roles
        [Authorize(Roles = "Chef, Administrador")]
        public async Task<IActionResult> EditarMenu()
        {
            ViewBag.caracteristicas = await _comidaRepository.ObtenerCaracteristicasComidas();
            ViewBag.modeloValido = true;
            ViewBag.modo = "Nada"; //Para la vista parcial
            return View();
        }



        //Vista parcial offcanvas del editar menú
        [Authorize(Roles = "Chef, Administrador")]
        public async Task<IActionResult> _ModificarComida(int id = 0)
        {
            ViewBag.caracteristicas = await _comidaRepository.ObtenerCaracteristicasComidas();
            ViewBag.modeloValido = true;
            ViewBag.anteriorImagen = "";
            ViewBag.listcaract = "[]";

            if (id is 0) return PartialView();
            else
            {
                Comida comida = await _comidaRepository.ObtenerPorId(id);


                ViewBag.comidaCarac = await _comidaRepository.ObtenerCaracteristicasPorComidaID(id);


                if (comida != null) return PartialView(comida);
                else return NotFound();
            }
        }



        //Para editar el stock de la comida seleccionada
        [Authorize(Roles = "Chef, Administrador")]
        public async Task<IActionResult> EditarStockComida(int idc, int nuevoStock)
        {
            Comida comida = await _comidaRepository.ObtenerPorId(idc);
            comida.Stock = nuevoStock;
            await _comidaRepository.EditarComida(comida);
            return RedirectToAction("EditarMenu");
        }


        //Para eliminar la comida seleccionada
        [Authorize(Roles = "Chef, Administrador")]
        public async Task<IActionResult> EliminarComida(int idcomida)
        {
            Comida comida = await _comidaRepository.ObtenerPorId(idcomida);
            _comidaRepository.EliminarImagen(comida.Imagen, _webHostEnvironment);
            await _comidaRepository.EliminarComida(comida);
            return RedirectToAction("EditarMenu");
        }


        //Para la acción de editar la comida seleccionada
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Chef, Administrador")]
        public async Task<IActionResult> EditarComida(
            [Bind("ID, Nombre", "Descripcion", "Categoria", "Precio",
            "MenuDelDia", "Stock")] Comida comida, string listaIndicescarac = "", 
            string urlant = "", string ChangeImage = "si")
        {
            ViewBag.caracteristicas = await _comidaRepository.ObtenerCaracteristicasComidas(); //Obtiene el listado general de caracteristicas, para los select
            ViewBag.modeloValido = true; //Para hacer aparecer automaticamente el offcanvas
            ViewBag.modo = "Editar"; //Para la vista parcial


            //El modelo es valido
            if (ModelState.IsValid)
            {
                if (ChangeImage == "si")
                {
                    comida.Imagen = _comidaRepository.CargarImagen(HttpContext, _webHostEnvironment);
                    _comidaRepository.EliminarImagen(urlant, _webHostEnvironment);
                }
                else comida.Imagen = urlant;
                

                await _comidaRepository.EditarComida(comida, listaIndicescarac);
                await _comidaRepository.Guardar();
                

                ViewBag.modeloValido = true;
                return RedirectToAction("EditarMenu");
            }
            ViewBag.modeloValido = false;
            ViewBag.anteriorImagen = urlant;
            ViewBag.listcaract = listaIndicescarac;
            ViewBag.comidaCarac = await _comidaRepository.ObtenerCaracteristicasPorComidaID(comida.ID);
            var comidas = await _comidaRepository.ObtenerComidas();

            return View("EditarMenu", comidas);

        }



        //Para agregar la comida a la base de datos
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Chef, Administrador")]
        public async Task<IActionResult> AgregarComida(
            [Bind("Nombre", "Descripcion", "Categoria", "Precio",
            "MenuDelDia", "Stock")] Comida comida, string listaIndicescarac = "")
        {
            var comidas = await _comidaRepository.ObtenerComidas();
            ViewBag.caracteristicas = await _comidaRepository.ObtenerCaracteristicasComidas(); //Obtiene el listado general de caracteristicas, para los select
            ViewBag.modeloValido = true; //Para hacer aparecer automaticamente el offcanvas
            ViewBag.modo = "Crear"; //Para la vista parcial


            //El modelo es valido
            if (ModelState.IsValid)
            {
                try //Para cuando la imagen no se manda
                {
                    comida.Imagen = _comidaRepository.CargarImagen(HttpContext, _webHostEnvironment);
                    await _comidaRepository.AgregarComida(comida, listaIndicescarac);
                    await _comidaRepository.Guardar();
                }
                catch (ArgumentOutOfRangeException)
                {
                    comida.Imagen = null;
                    ViewBag.imagenNula = "Debes subir un archivo";
                    ViewBag.modeloValido = false;
                }


                ViewBag.modeloValido = true;
                return RedirectToAction("EditarMenu");
            }
            ViewBag.modeloValido = false;
            return View("EditarMenu", comidas);
        }

        #endregion

        #region CaracteristicasComida
        [Authorize(Roles = "Chef, Administrador")]
        public async Task<IActionResult> EditarCaracteristica(int id)
        {
			CaracteristicaComida cc = await _comidaRepository.ObtenerCaracteristicaPorID(id);
			if (cc is not null) //Se encontró el elemento
			{
                return RedirectToAction("CaracteristicaComida", cc);
			}
			else return NotFound();

		}

        [Authorize(Roles = "Chef, Administrador")]
        public async Task<IActionResult> EliminarCaracteristica(int id)
        {
            CaracteristicaComida cc = await _comidaRepository.ObtenerCaracteristicaPorID(id);
            if (cc is not null) //Se encontró el elemento
            {
                await _comidaRepository.EliminarCaracteristica(cc);
				CookieOptions cookie = new CookieOptions();
				Response.Cookies.Append("mensaje", "La característica fue eliminada correctamente", cookie);
				return RedirectToAction("CaracteristicaComida");
            }
            else return NotFound();
        }

        //GET
        [Authorize(Roles = "Chef, Administrador")]
        public async Task<IActionResult> CaracteristicaComida(CaracteristicaComida cc = null)
        {
            ViewBag.caracteristicas = await _comidaRepository.ObtenerCaracteristicasComidas();
			string estado = Request.Cookies["mensaje"];
            if(estado is not null)
            {
                ViewBag.resultado = estado;
				Response.Cookies.Delete("mensaje");
			}


            if (cc.Nombre is null) //No editable, solo dirije al formulario para registrar datos nuevos
            {
                ViewBag.editable = false;
                return View();
            }
			else //Editable
			{
				ViewBag.editable = true;
				return View(model: cc);
            }
        }


        [HttpPost]
        [Authorize(Roles = "Chef, Administrador")]
        public async Task<IActionResult> CaracteristicaComida(
            [Bind("Nombre, Detalle, Precio")] CaracteristicaComida caracteristica, int idc=0, string edit = "False")
        {
			ViewBag.editable = edit; //Para que esté en un estado de editable o no editable
			bool modeloValido = true; //Para comprobar si el nombre se repite o no
            if (idc != 0) //Colocar id de manera auxiliar
            {
                caracteristica.Id = idc;
            }


            if(!(edit == "True"))
            {
                //Evitar que se repita el mismo nombre de caracteristica
                //Esto no aplica cuando el elemento va a ser editado
                try
                {
                    if (!await _comidaRepository.CaracteristicaNombreUnico(caracteristica.Nombre))
                    {
                        modeloValido = false;
                        ViewBag.nombreCaracteristicaRepetido = "El nombre debe ser único para cada característica";
                    }
                }
                catch(Exception ex)
                {

                }
			}



			if (ModelState.IsValid && modeloValido) //Modelo completamente válido
            {
                CookieOptions cookie = new CookieOptions();
                if (!(edit == "True")) //No editable mode
                {
                    //Agregar elemento
					await _comidaRepository.AgregarCaracteristica(caracteristica);
					ViewBag.caracteristicas = await _comidaRepository.ObtenerCaracteristicasComidas();
					Response.Cookies.Append("mensaje", "La característica fue registrada correctamente", cookie);
					return RedirectToAction("CaracteristicaComida");
				}
                else
                {
                    //Actualizar elemento
					await _comidaRepository.ActualizarCaracteristica(caracteristica);
					ViewBag.editable = false;
					ViewBag.caracteristicas = await _comidaRepository.ObtenerCaracteristicasComidas();
					Response.Cookies.Append("mensaje", "La característica fue editada correctamente", cookie);
					return RedirectToAction("CaracteristicaComida");
				}

			}
			ViewBag.caracteristicas = await _comidaRepository.ObtenerCaracteristicasComidas();


            if (edit == "True") return View(caracteristica);
            else return View();
        }
        public async Task<IActionResult> DetalleComida(int id)
        {
            var comidas = await _comidaRepository.ObtenerComidas();
            
            Comida comidaEncontrada = null;
            foreach (var comida in comidas)
            {
                if (comida.ID == id)
                {
                    comidaEncontrada = comida;
                    break;
                }
            }

            if (comidaEncontrada == null)
            {
                return NotFound();
            }

            int idUser = int.Parse(User.FindFirstValue("ID"));

            ViewData["UserId"] = idUser;
            ViewData["ComidaId"] = comidaEncontrada.ID;

            List<Comentario> comentarios = await _context.Comentarios
        .Include(c => c.Usuario)
        .Where(c => c.Comida.ID == comidaEncontrada.ID)
        .ToListAsync();
            var viewModel = new DetalleComidaViewModel
            {
                ComidaEncontrada = comidaEncontrada,
                Comentarios = comentarios
            };

            return View(viewModel);
        }
        [HttpPost]
        public async Task<IActionResult> Comentar(int UserId, int ComidaId, string Comentario)
        {
            if (string.IsNullOrWhiteSpace(Comentario))
            {
          
                ModelState.AddModelError("Comentario", "El comentario no puede estar vacío.");
                return RedirectToAction("DetalleComida", new { id = ComidaId });
            }

            Usuario userr = await _context.Usuarios.FirstOrDefaultAsync(c => c.Id == UserId);
            Comida comida = await _context.Comidas.FirstOrDefaultAsync(c => c.ID == ComidaId);

            if (userr == null)
            {
              
                return NotFound("Usuario no encontrado");
            }
           

            if (comida == null)
            {
      
                return NotFound("Comida no encontrada");
            }

 
            var nuevoComentario = new Comentario
            {
                Usuario = userr,
                Comida = comida,
                Contenido = Comentario,
                Fecha = DateTime.Now
            };

            _context.Comentarios.Add(nuevoComentario);
            await _context.SaveChangesAsync();
    
            // Redirigir de vuelta a la vista DetalleComida
            return RedirectToAction("DetalleComida", new { id = ComidaId });
        }
        #endregion

    }
}
