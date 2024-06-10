using BiblioGest;
using BiblioGest.Model;
using MongoDB.Driver;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TestProject
{
    public class Tests
    {
        private IMongoCollection<Producto> _productCollection;
        private IMongoCollection<Chat> _chatCollection;
        private IMongoCollection<ChatMessage> _chatMessageCollection;
        private IMongoCollection<Usuario> _userCollection;

        [SetUp]
        public void Setup()
        {
            var client = new MongoClient("mongodb://localhost:27017");
            var database = client.GetDatabase("libreria");
            _productCollection = database.GetCollection<Producto>("productos");
            _chatCollection = database.GetCollection<Chat>("chats");
            _chatMessageCollection = database.GetCollection<ChatMessage>("chatMessages");
            _userCollection = database.GetCollection<Usuario>("usuarios");

            // Limpiar las colecciones antes de cada prueba
            _productCollection.DeleteMany(Builders<Producto>.Filter.Empty);
            _chatCollection.DeleteMany(Builders<Chat>.Filter.Empty);
            _chatMessageCollection.DeleteMany(Builders<ChatMessage>.Filter.Empty);
            _userCollection.DeleteMany(Builders<Usuario>.Filter.Empty);

            // Configurar las variables globales
            GlobalVariables.collectionProductos = _productCollection;
            GlobalVariables.collectionUsuarios = _userCollection;
            GlobalVariables.collectionReviews = database.GetCollection<PuntuacionProducto>("productosReview");
        }

        [Test]
        public void CodigoReferencia_Test()
        {
            var codigosExistentes = new HashSet<string>();

            for (int i = 0; i < 50; i++)
            {
                var producto = new Producto(nombre: $"Producto{i}");
                var codigoReferencia = producto.GenerarCodigoReferencia();

                // Verificar que el código no existe previamente en la base de datos
                var filtro = Builders<Producto>.Filter.Eq(p => p._id, codigoReferencia);
                var productoExistente = _productCollection.Find(filtro).FirstOrDefault();

                // Verificar que el código no ha sido generado anteriormente en esta prueba
                Assert.IsFalse(codigosExistentes.Contains(codigoReferencia), $"El código de referencia '{codigoReferencia}' ya fue generado.");

                // Verificar que el código no existe en la base de datos
                Assert.IsNull(productoExistente, $"El código de referencia '{codigoReferencia}' ya existe en la base de datos.");

                // Insertar el producto en la base de datos
                producto._id = codigoReferencia;
                _productCollection.InsertOne(producto);

                // Agregar el código al conjunto de códigos generados
                codigosExistentes.Add(codigoReferencia);
            }

            Assert.Pass();
        }

        [Test]
        public async Task EliminarMensajesPasados_Test()
        {
            // Crear usuario y asignarlo a GlobalVariables.UsuarioActual
            var usuario = new Usuario("TestUser");
            GlobalVariables.UsuarioActual = usuario;

            _userCollection.InsertOne(usuario);

            // Crear y insertar mensajes en la base de datos
            var currentTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var pastTimestamp = currentTimestamp - 10000; // Un tiempo en el pasado
            var futureTimestamp = currentTimestamp + 10000; // Un tiempo en el futuro

            var messagePast = new ChatMessage { Usuario = usuario._id, ClienteNombre = usuario._id, Hora = pastTimestamp };
            var messageFuture = new ChatMessage { Usuario = usuario._id, ClienteNombre = usuario._id, Hora = futureTimestamp };

            _chatMessageCollection.InsertOne(messagePast);
            _chatMessageCollection.InsertOne(messageFuture);

            // Eliminar mensajes pasados del UsuarioActual
            await GlobalVariables.EliminarMensajesPasados();

            // Verificar que solo los mensajes pasados han sido eliminados
            var remainingMessages = _chatMessageCollection.Find(Builders<ChatMessage>.Filter.Eq(m => m.ClienteNombre, usuario._id)).ToList();

            Assert.AreEqual(1, remainingMessages.Count);
            Assert.AreEqual(futureTimestamp, remainingMessages[0].Hora);
        }

        [Test]
        public void ObtenerProductosReservadosPorUsuario_Test()
        {
            // Crear productos y asignar uno a un usuario
            var producto1 = new Producto(nombre: "Producto1", _id: "P1");
            producto1.ReservadoPor = "Usuario1";
            var producto2 = new Producto(nombre: "Producto2", _id: "P2");
            producto2.ReservadoPor = "Usuario2";

            _productCollection.InsertOne(producto1);
            _productCollection.InsertOne(producto2);

            // Obtener productos reservados por Usuario1
            var productos = Producto.ObtenerProductosReservadosPorUsuario("Usuario1");

            Assert.AreEqual(1, productos.Length);
            Assert.AreEqual("P1", productos[0]._id);
        }


        [Test]
        public void ConvertirDesdeUnix_Test()
        {
            // Timestamp para 1 de enero de 2022
            int unixTimestamp = 1640995200;

            // Convertir timestamp
            var fecha = Producto.ConvertirDesdeUnix(unixTimestamp);

            Assert.AreEqual("01/01/2022", fecha);
        }

        [Test]
        public void ObtenerIdUltimoProducto_Test()
        {
            // Crear productos y asignarlos a la colección
            var producto1 = new Producto(nombre: "Producto1", _id: "R-0001");
            var producto2 = new Producto(nombre: "Producto2", _id: "R-0002");

            _productCollection.InsertOne(producto1);
            _productCollection.InsertOne(producto2);

            // Obtener el ID del último producto
            var ultimoId = Producto.ObtenerIdUltimoProducto();

            Assert.AreEqual("R-0002", ultimoId);
        }
        [Test]
        public void Insertar_DebeInsertUser()
        {
            var usuario = new Usuario("TestUser", Usuario.Roles.Cliente, "password123");

            usuario.Insertar();

            var insertedUser = _userCollection.Find(Builders<Usuario>.Filter.Eq(u => u._id, "TestUser")).FirstOrDefault();

            Assert.IsNotNull(insertedUser);
            Assert.AreEqual("TestUser", insertedUser._id);
            Assert.AreEqual(Usuario.HashPassword("password123"), Usuario.HashPassword(insertedUser.Contraseña));
            Assert.AreEqual(Usuario.Roles.Cliente, insertedUser.Rol);
        }

        [Test]
        public void Login_DebeRetornarTrueParaValidUser()
        {
            var usuario = new Usuario("TestUser", Usuario.Roles.Cliente, "password123");
            usuario.Insertar();

            var result = usuario.Login();

            Assert.IsTrue(result);
        }

        [Test]
        public void Login_DebeRetornarFalseParaUsuarioNoValid()
        {
            var usuario = new Usuario("NonExistentUser", Usuario.Roles.Cliente, "password123");

            var result = usuario.Login();

            Assert.IsFalse(result);
        }

        [Test]
        public void Registrarse_DebeRetornar1ParaNuevoUsuario()
        {
            var usuario = new Usuario("NewUser", Usuario.Roles.Cliente, "password123");

            var result = usuario.Registrarse();

            Assert.AreEqual(1, result);
        }

        [Test]
        public void Registrarse_DebeRetornar0ParaUsuarioExistente()
        {
            var usuario = new Usuario("ExistingUser", Usuario.Roles.Cliente, "password123");
            usuario.Insertar();

            var result = usuario.Registrarse();

            Assert.AreEqual(0, result);
        }

        [Test]
        public void Eliminar_DebeBorrarUser()
        {
            var usuario = new Usuario("UserToDelete", Usuario.Roles.Cliente, "password123");
            usuario.Insertar();

            var result = usuario.Eliminar();

            Assert.AreEqual(1, result);
            var deletedUser = _userCollection.Find(Builders<Usuario>.Filter.Eq(u => u._id, "UserToDelete")).FirstOrDefault();
            Assert.IsNull(deletedUser);
        }

        [Test]
        public void ObtenerUsuarioPorId_DebeRetornarUsuarioCorrecto()
        {
            var usuario = new Usuario("UserToFind", Usuario.Roles.Cliente, "password123");
            usuario.Insertar();

            var foundUser = Usuario.ObtenerUsuarioPorId("UserToFind");

            Assert.IsNotNull(foundUser);
            Assert.AreEqual("UserToFind", foundUser._id);
        }

        [Test]
        public void ObtenerUsuarios_DebeRetornarTodosUsuarios()
        {
            var usuario1 = new Usuario("User1", Usuario.Roles.Cliente, "password123");
            var usuario2 = new Usuario("User2", Usuario.Roles.Cliente, "password123");
            usuario1.Insertar();
            usuario2.Insertar();

            var usuarios = Usuario.ObtenerUsuarios();

            Assert.AreEqual(2, usuarios.Count);
        }

        [Test]
        public void Consulta_DebeRetornarSeleccionados()
        {
            var usuario1 = new Usuario("User1", Usuario.Roles.Cliente, "password123");
            var usuario2 = new Usuario("User2", Usuario.Roles.Bibliotecario, "password123");
            usuario1.Insertar();
            usuario2.Insertar();

            var usuarios = Usuario.Consulta("User1", Usuario.Roles.NoDefinido);

            Assert.AreEqual(1, usuarios.Length);
            Assert.AreEqual("User1", usuarios[0]._id);
        }

        [Test]
        public void SubirRol_DebeIncrementarRol()
        {
            var usuario = new Usuario("UserToPromote", Usuario.Roles.Cliente, "password123");
            usuario.Insertar();

            var result = usuario.SubirRol();

            Assert.AreEqual(1, result);
            var updatedUser = Usuario.ObtenerUsuarioPorId("UserToPromote");
            Assert.AreEqual(Usuario.Roles.Bibliotecario, updatedUser.Rol);
        }

        [Test]
        public void NuevaClave_DebeGenerarNuevaPassword()
        {
            var usuario = new Usuario("UserToChangePassword", Usuario.Roles.Cliente, "password123");
            usuario.Insertar();

            var result = usuario.NuevaClave();

            Assert.AreEqual(1, result);
            var updatedUser = Usuario.ObtenerUsuarioPorId("UserToChangePassword");
            Assert.AreNotEqual("password123", updatedUser.Contraseña);
        }

        [Test]
        public void CambiarPassword_DebeActualizarPassword()
        {
            var usuario = new Usuario("UserToUpdatePassword", Usuario.Roles.Cliente, "password123");
            usuario.Insertar();

            var result = usuario.CambiarPassword("newpassword123");

            //Assert.AreEqual(1, result);
            var updatedUser = Usuario.ObtenerUsuarioPorId("UserToUpdatePassword");
            Assert.AreEqual(Usuario.HashPassword("newpassword123"), updatedUser.Contraseña);
        }

        [Test]
        public void BajarRol_DebeDecrementarRol()
        {
            var usuario = new Usuario("UserToDemote", Usuario.Roles.Bibliotecario, "password123");
            usuario.Insertar();

            var result = usuario.BajarRol();

            Assert.AreEqual(1, result);
            var updatedUser = Usuario.ObtenerUsuarioPorId("UserToDemote");
            Assert.AreEqual(Usuario.Roles.Cliente, updatedUser.Rol);
        }

        [Test]
        public void VerificarLogin_DebeRetornarValoresCorrectos()
        {
            var usuario = new Usuario("LoginUser", Usuario.Roles.Cliente, "password123");
            usuario.Insertar();

            var validLogin = Usuario.VerificarLogin("LoginUser", "password123");
            var invalidPassword = Usuario.VerificarLogin("LoginUser", "wrongpassword");
            var nonExistentUser = Usuario.VerificarLogin("NonExistentUser", "password123");

            Assert.AreEqual(1, validLogin);
            Assert.AreEqual(-2, invalidPassword);
            Assert.AreEqual(-1, nonExistentUser);
        }

        [Test]
        public void IncrementarProductos_DebeIncrementarContador()
        {
            var usuario = new Usuario("UserToIncrementProduct", Usuario.Roles.Cliente, "password123");
            usuario.Insertar();

            var newProductCount = usuario.IncrementarProductos();

            Assert.AreEqual(1, newProductCount);
            var updatedUser = Usuario.ObtenerUsuarioPorId("UserToIncrementProduct");
            Assert.AreEqual(1, updatedUser.Productos);
        }
    }
}
