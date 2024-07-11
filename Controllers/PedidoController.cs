using Delivery.Domain.Food;
using Delivery.Domain.Order;
using Delivery.Repositories.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace Delivery.Controllers
{
    public class PedidoController : Controller
    {
        private readonly IComidaRepository _comidaRepository;
        private readonly IPedidoRepository _pedidoRepository;
        private readonly IUsuarioRepository _usuarioRepository;
        public PedidoController(IComidaRepository comidaRepository,
            IPedidoRepository pedidoRepository,
            IUsuarioRepository usuarioRepository)
        {
            _comidaRepository = comidaRepository;
            _pedidoRepository = pedidoRepository;
            _usuarioRepository = usuarioRepository;

        }



        [Authorize(Roles = "Cliente")]
        public IActionResult CrearPedido()
        {
            //Almacenar temporalmente la información de lo que se pidio
            var lista = TempData["lista"];
            TempData["lista_post"] = TempData["lista"];
            TempData["PrecioTotal_post"] = TempData["PrecioTotal"];

            //En caso de que se refresque la pagina o se acceda a esta vista sin info del pedido
            if (lista == null) return RedirectToAction("VerMenu", "Comida");
            return View();
        }


        [HttpPost]
        [AutoValidateAntiforgeryToken]
        [Authorize(Roles = "Cliente")]
        public async Task<IActionResult> CrearPedido([Bind("Direccion, MetodoPago")] Pedido pedido)
        {
            //Recuperar y restaurar data del pedido
            string comida_pedido_log = TempData["lista_post"].ToString();
            string nombre_cliente = User.FindFirst("Nombre").Value + " " + User.FindFirst("Apellido").Value;

            float precio = float.Parse(TempData["PrecioTotal_post"].ToString().Replace('.', ','));


            //Guardar data
            Direccion address = pedido.Direccion;
            MetodoPago? method_pay = pedido.MetodoPago;

            await _pedidoRepository.Registrar_Direccion(address);
            MetodoPago? mp_aux = await _pedidoRepository.Buscar_MetodoPago(mp => mp.Numero == method_pay.Numero);


            //Evitar que se guarde un método de pago siendo este uno de efectivo
            if (method_pay.Tipo != TipoMetodoPago.Efectivo)
            {
                if (mp_aux is not null)
                {
                    mp_aux.NombreTarjeta = method_pay.NombreTarjeta;
                    mp_aux.CVV = method_pay.CVV;
                    mp_aux.fechaExpiracion = method_pay.fechaExpiracion;
                    await _pedidoRepository.ActualizarMetodoPago(mp_aux);
                }
                else await _pedidoRepository.Registrar_MetodoPago(method_pay);
            }

            pedido.IdCliente = int.Parse(User.FindFirst("Id").Value);
            pedido.Cliente = nombre_cliente;
            pedido.Telefono = User.FindFirst("Telefono").Value;
            pedido.Fecha_Inicio = DateTime.Now;
            pedido.Estado = EstadoPedido.En_Proceso;
            pedido.IdDireccion = address.Id;
            pedido.Total = precio;

            //Cambiar valor a null
            if (method_pay.Tipo == TipoMetodoPago.Efectivo)
            {
                pedido.IdMetodoPago = null;
                pedido.MetodoPago = null;
            }
            else
            {
                if (mp_aux is null)
                {
                    pedido.IdMetodoPago = method_pay.Id;
                }
                else
                {
                    pedido.IdMetodoPago = mp_aux.Id;
                    pedido.MetodoPago = mp_aux;
                }
            }



            await _pedidoRepository.Agregar(pedido);
            await _pedidoRepository.Guardar();

            await _comidaRepository.Registrar_Comida_Log(comida_pedido_log, pedido.Codigo);

            return RedirectToAction("TablaPedido");
        }


        [Authorize(Roles = "Administrador, Repartidor, Cliente")]
        public async Task<IActionResult> TablaPedido()
        {
            //Inicia vacio
            IEnumerable<Pedido> lista_pedidos = Enumerable.Empty<Pedido>();

            if (User.IsInRole("Cliente"))
            {
                int ID_Cliente = int.Parse(User.FindFirst("ID").Value);
                lista_pedidos = await _pedidoRepository.ObtenerTodos(x => x.IdCliente == ID_Cliente);
            }
            else if (User.IsInRole("Repartidor") || User.IsInRole("Administrador"))
            {
                lista_pedidos = await _pedidoRepository.ObtenerTodos();
            }

            foreach (var item in lista_pedidos)
            {
                item.Direccion = await _pedidoRepository.Buscar_Direccion(d => d.Id == item.IdDireccion);
                item.MetodoPago = await _pedidoRepository.Buscar_MetodoPago(mp => mp.Id == item.IdMetodoPago);
            }

            return View(lista_pedidos);
        }

        public async Task<IActionResult> _DetallePedido(int? idPedido)
        {
            var comidas_pedido = await _comidaRepository.Obtener_comidaPedidoId(idPedido.GetValueOrDefault());
            ViewBag.Comida_pedidoJSON = _pedidoRepository.DeserealizarJSON(comidas_pedido.Contenido);
            var pedido = await _pedidoRepository.ObtenerPorId(idPedido.GetValueOrDefault());
            pedido.Direccion = await _pedidoRepository.Buscar_Direccion(d => d.Id == pedido.IdDireccion);
            return PartialView(pedido);
        }




        #region Cambiar estado del pedido

        [Authorize(Roles = "Repartidor")]
        public async Task<IActionResult> Aceptar_Pedido(int idPedido)
        {
            var pedido = await _pedidoRepository.ObtenerPorId(idPedido);
            int idRepartidor = int.Parse(User.FindFirst("ID").Value);

            if (pedido.IdRepartidor is null)
            {
                //Indica si puede aceptar el pedido, validando que no tenga otro pedido pendiente
                if(await _pedidoRepository.Aceptar_Pedido(idRepartidor))
                {
                    pedido.IdRepartidor = idRepartidor;
                    pedido.Repartidor = User.FindFirst("Nombre").Value + " " + User.FindFirst("Apellido").Value;
                    pedido.Estado = EstadoPedido.Aceptado;
                    _pedidoRepository.Actualizar(pedido);
                    await _pedidoRepository.Guardar();
                    return Json(null);
                }
                else return Json("Tienes un pedido por terminar");
            }
            else return Json("Este pedido ya ha sido elegido");
        }

        [Authorize(Roles = "Repartidor, Cliente")]
        public async Task<IActionResult> Terminar_Pedido(int idPedido)
        {
            int idUsuario = int.Parse(User.FindFirst("ID").Value);
            var pedido = await _pedidoRepository.ObtenerPorId(idPedido);

            if(pedido.IdCliente == idUsuario && pedido.Estado == EstadoPedido.Pendiente)
            {
                pedido.Estado = EstadoPedido.Terminado;
                pedido.Fecha_Fin = DateTime.Now;
                _pedidoRepository.Actualizar(pedido);
                await _pedidoRepository.Guardar();
                return Json(null);
            }
            else if (pedido.IdRepartidor == idUsuario)
            {
                pedido.Estado = EstadoPedido.Pendiente;
                _pedidoRepository.Actualizar(pedido);
                await _pedidoRepository.Guardar();
                return Json(null);
            }
            else return Json("Ha ocurrido un error a la hora de terminar el pedido");
        }

        [Authorize(Roles = "Repartidor, Cliente")]
        public async Task<IActionResult> Cancelar_Pedido(int idPedido, string detalle)
        {
            var pedido = await _pedidoRepository.ObtenerPorId(idPedido);
            int idUsuario = int.Parse(User.FindFirst("ID").Value);

            if (User.IsInRole("Repartidor"))
            {
                pedido.Estado = EstadoPedido.Cancelado;
                pedido.Detalle = detalle;
                pedido.Fecha_Fin = DateTime.Now;
                pedido.IdRepartidor = idUsuario;
                pedido.Repartidor = User.FindFirst("Nombre").Value + " " + User.FindFirst("Apellido").Value;
                _pedidoRepository.Actualizar(pedido);
                await _pedidoRepository.Guardar();
                return Json(null);
            }
            else if (pedido.IdCliente == idUsuario)
            {
                pedido.Estado = EstadoPedido.Cancelado;
                pedido.Detalle = detalle;
                pedido.Fecha_Fin = DateTime.Now;
                _pedidoRepository.Actualizar(pedido);
                await _pedidoRepository.Guardar();
                return Json("Se ha cancelado el pedido");
            }
            else return Json("No puedes cancelar un pedido ajeno");

        }


        [Authorize(Roles = "Repartidor")]
        public async Task<IActionResult> Dejar_Pedido(int idPedido)
        {
            int idRepartidor = int.Parse(User.FindFirst("ID").Value);
            var pedido = await _pedidoRepository.ObtenerPorId(idPedido);

            if (pedido.IdRepartidor == idRepartidor)
            {
                pedido.IdRepartidor = null;
                pedido.Repartidor = null;
                pedido.Estado = EstadoPedido.En_Proceso;
                _pedidoRepository.Actualizar(pedido);
                await _pedidoRepository.Guardar();
                return Json(null);
            }
            else return Json("No puede dejar un pedido que no haya sido aceptado por usted");
        }

        #endregion
    }
}

