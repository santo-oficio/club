using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Club.Adapters;
using Club.Utils;
using Club.WsConsumicion;
using Club.WsProductos;
using Club.WsSocio;
using Club.WsTablaAuxiliarSocio;
using Square.Picasso;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Club
{
    [Activity(Label = "Sala 2", Theme = "@android:style/Theme.Holo.Light.NoActionBar", WindowSoftInputMode = SoftInput.StateHidden)]
    public class RetiradaSala2 : Activity
    {
        private Button buttonBuscar, buttonRetirar;
        private EditText editTextBuscar;
        private Socio _socio;
        private TablaAuxiliarSocio _tablaAuxSocio;
        private ProgressDialog _dialog;

        int RESULT = 0;

        private decimal totalARetirar = 0;
        private decimal aprovechable, cantidadAcumulada;
        private decimal totalConsumidoMes, totalAcumulado;
        private Button buttonCantidad1, buttonCantidad2, buttonCantidad3, buttonCantidad4, buttonAnhadirACarrito, buttonLimpiar;
        private Spinner spinner;
        private ListView listViewArticulos;
        private EditText editTextLimite, editTextDescripcion, editTextPrecio, editTextAprovechable, editTextSocio, editTextArticulo, editTextTotalAConsumir, editTextTotalConsumidoMes, editTextParcial;
        private Articulo _articulo;
        private ScrollView scrollView;
        private ImageView imageFoto;
        private bool _existeSocio;
        private bool _vuelveAMostrar = true;
        private bool _listViewArticulosActivo = true;
        private Familia _familia;
        private ToggleButton chkMostrarListaCompra;
        private Button buttonBorrarSoloCantidades;
        private List<Familia> _listFamilias;
        private List<Articulo> _listArticulos;
        private ListViewFamiliaAdapter _adapterFamilias;
        private ListViewArticuloAdapter _adapterArticulos;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            SetContentView(Resource.Layout.RetiradaSala2);

            InitComponents();

            editTextBuscar.KeyPress += OnEnterKeyPressed;

            buttonBuscar.Click += new EventHandler(buttonBuscar_Click);
            spinner.ItemSelected += spinner_ItemSelected;

            listViewArticulos.ItemClick += delegate (object sender, AdapterView.ItemClickEventArgs position)
            {
                if (_listViewArticulosActivo && _existeSocio)
                {
                    _articulo = _listArticulos[position.Position];
                    MuestraSocio();
                    buttonCantidad1.Text = "" + Redondeo.DosDecimales(_articulo.Cantidad1);
                    buttonCantidad2.Text = "" + Redondeo.DosDecimales((decimal)_articulo.Cantidad2);
                    buttonCantidad3.Text = "" + Redondeo.DosDecimales((decimal)_articulo.Cantidad3);
                    buttonCantidad4.Text = "" + Redondeo.DosDecimales((decimal)_articulo.Cantidad4);
                    editTextArticulo.Text = "\t" + _articulo.Nombre;
                    //editTextParcial.setText("\t" + cantidadAcumulada);
                    editTextPrecio.Text = "\t" + Redondeo.DosDecimales(_articulo.Precio);
                    editTextDescripcion.Text = "\t" + _articulo.Descripcion.Trim();
                    buttonCantidad1.RequestFocus();
                    scrollView.ScrollTo(0, 0);
                }
                else
                {
                    if (!_listViewArticulosActivo)
                    {
                        Preguntar(position.Position);
                    }
                }
            };

            buttonCantidad1.Click += delegate
            {
                if (_existeSocio && _articulo != null && _listViewArticulosActivo)
                {
                    decimal cantidad = Convert.ToDecimal(buttonCantidad1.Text);
                    CalcularCantidad(cantidad, _articulo.Precio);
                }
            };

            buttonCantidad2.Click += delegate
            {
                if (_existeSocio && _articulo != null && _listViewArticulosActivo)
                {
                    decimal cantidad = Convert.ToDecimal(buttonCantidad2.Text);
                    CalcularCantidad(cantidad, _articulo.Precio);
                }
            };

            buttonCantidad3.Click += delegate
            {
                if (_existeSocio && _articulo != null && _listViewArticulosActivo)
                {
                    decimal cantidad = Convert.ToDecimal(buttonCantidad3.Text);
                    CalcularCantidad(cantidad, _articulo.Precio);
                }
            };

            buttonCantidad4.Click += delegate
            {
                if (_existeSocio && _articulo != null && _listViewArticulosActivo)
                {
                    decimal cantidad = Convert.ToDecimal(buttonCantidad4.Text);
                    CalcularCantidad(cantidad, _articulo.Precio);
                }
            };

            buttonAnhadirACarrito.Click += delegate
            {
                if (cantidadAcumulada > 0 && _socio != null)
                {
                    Retirada retirada = new Retirada();
                    retirada.Total = cantidadAcumulada;
                    retirada.PrecioArticulo = _articulo.Precio;
                    if (totalConsumidoMes > _socio.ConsumicionMaxima)
                    {
                        editTextTotalConsumidoMes.Text = "\t" + Redondeo.DosDecimales(_socio.ConsumicionMaxima);
                    }
                    else
                    {
                        editTextTotalConsumidoMes.Text = "\t" + Redondeo.DosDecimales(totalConsumidoMes);
                    }
                    //retirada.Aprovechable(aprovechable);
                    retirada.Cantidad = totalAcumulado;

                    ListaCompraAction.anhadirArticuloAcarrito(retirada, _socio, _tablaAuxSocio, _familia, _articulo);

                    MuestraSocio();

                    Toast.MakeText(this, "Se ha añadido al carrito", ToastLength.Short).Show();
                }
                else
                {
                    Club.Utils.ShowDialog.SimpleDialogError(this, "Error. La cantidad no puede ser cero.");
                }
            };

            buttonLimpiar.Click += delegate
            {
                MuestraSocio();
            };

            buttonBorrarSoloCantidades.Click += delegate
            {
                if (_socio != null)
                {
                    editTextParcial.Text = "";
                    editTextTotalAConsumir.Text = "";
                    cantidadAcumulada = 0;
                    totalAcumulado = 0;
                    //aprovechable = ListaCompraAction.getAprovechable();
                    if (ListaCompraAction.getlistArticulos() != null)
                    {
                        editTextAprovechable.Text = "\t" + Redondeo.DosDecimales((decimal)ListaCompraAction.getAprovechable()) + " créditos.";
                        aprovechable = (decimal)ListaCompraAction.getAprovechable();
                    }
                    else
                    {
                        editTextAprovechable.Text = "\t" + Redondeo.DosDecimales((decimal)_tablaAuxSocio.Aprovechable) + " créditos.";
                        aprovechable = (decimal)_tablaAuxSocio.Aprovechable;
                    }
                }
            };

            chkMostrarListaCompra.Click += delegate
            {
                if (chkMostrarListaCompra.Checked)
                {
                    if (ListaCompraAction.getlistArticulos() != null)
                    {
                        _listViewArticulosActivo = false;
                        listViewArticulos.Adapter = null;
                        listViewArticulos.Adapter = new ListaCompraAdapter(this);
                        //listViewArticulos.LayoutParameters = new LinearLayout.LayoutParams(LinearLayout.LayoutParams.MatchParent, _listArticulos.Count * 75 + 200);
                        Util.SetListViewHeightBasedOnChildren(listViewArticulos);
                    }
                }
                else
                {
                    if (ListaCompraAction.getlistArticulos() != null)
                    {
                        _listViewArticulosActivo = true;
                        listViewArticulos.Adapter = null;
                        listViewArticulos.Adapter = new ListViewArticuloAdapter(this, Resources, _listArticulos);
                        listViewArticulos.LayoutParameters = new LinearLayout.LayoutParams(LinearLayout.LayoutParams.MatchParent, _listArticulos.Count * 75 + 200);

                        Util.SetListViewHeightBasedOnChildren(listViewArticulos);
                    }
                }
            };

            buttonRetirar.Click += delegate
            {
                if (ListaCompraAction.getlistArticulos() == null)
                {
                    Club.Utils.ShowDialog.SimpleDialogError(this, "Error. No existe ninguna línea de pedido");
                }
                else if (!ListaCompraAction.isCorrecto())
                {
                    Club.Utils.ShowDialog.SimpleDialogError(this, "El socio no dispone de suficiente aprovechable");
                }
                else
                {
                    MuestraListaCompra();
                }
            };
        }

        private async void OnEnterKeyPressed(object sender, View.KeyEventArgs e)
        {
            if (e.KeyCode == Keycode.Enter && e.Event.Action == KeyEventActions.Up)
            {
                if (editTextBuscar == null)
                {
                    editTextBuscar = FindViewById<EditText>(Resource.Id.editTextAccesoBuscarSocio);
                }

                if (editTextBuscar.Text.Length > 0)
                {
                    if (ListaCompraAction.getlistArticulos() != null)
                    {
                        Dialog dialog = null;
                        AlertDialog.Builder alert = new AlertDialog.Builder(this);
                        alert.SetCancelable(false);
                        alert.SetTitle("Atención");
                        alert.SetMessage("\tHay pedidos pendientes. Si continúa se perderán.\tContinuar?");

                        alert.SetPositiveButton("Si", (senderAlert, args) =>
                        {
                            ListaCompraAction.vaciarCarrito();
                            this.RunOnUiThread(async () =>
                            {
                                await Buscar();
                            });
                        });
                        alert.SetNegativeButton("No", (senderAlert, args) =>
                        {
                        });

                        dialog = alert.Create();
                        dialog.Show();
                    }
                    else
                    {
                        await Buscar();
                    }
                }

            }
            else
            {
                e.Handled = false;
            }
        }

        protected override void OnActivityResult(int requestCode, [GeneratedEnum] Result resultCode, Intent data)
        {
            base.OnActivityResult(requestCode, resultCode, data);

            if (resultCode == Result.Canceled)
            {
                Toast.MakeText(this, "Cancelado", ToastLength.Long).Show();
            }
            else if (resultCode == Result.Ok)
            {
                string code = data.GetStringExtra("RESULT_STRING");
                //Toast.makeText(this, "Se ha insertado correctamente en la BD", Toast.LENGTH_LONG).show();
                MuestraCode(code);
            }
            else
            {
                Toast.MakeText(this, "Hubo un error al procesar la compra", ToastLength.Long).Show();
            }
        }

        protected override void OnPause()
        {
            base.OnPause();
            if (!ListaCompraAction.getFirma())
            {
                ListaCompraAction.vaciarCarrito();
                this.Finish();
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            ListaCompraAction.vaciarCarrito();
        }

        private void MuestraCode(string code)
        {
            Dialog dialog = null;
            AlertDialog.Builder alert = new AlertDialog.Builder(this);
            alert.SetCancelable(false);
            alert.SetTitle("Información");
            alert.SetMessage("\tRetirada realizada con éxito.\n\tCódigo de retirada: " + code.Substring(5, 5) + ListaCompraAction.imprimeLista() + "\nSocio: " + Texto.GetString(_socio.Codigo));

            alert.SetPositiveButton("OK", (senderAlert, args) =>
            {
                ListaCompraAction.firmarCompra(false);
                ListaCompraAction.vaciarCarrito();
                dialog.Dismiss();
                this.Finish();
            });

            dialog = alert.Create();
            dialog.Show();
        }

        private void MuestraListaCompra()
        {
            Dialog dialog = null;
            AlertDialog.Builder alert = new AlertDialog.Builder(this);
            alert.SetCancelable(false);
            alert.SetTitle("Atención");
            alert.SetMessage("¿Desea realizar la retirada de los siguientes productos?" + "\n\n" + ListaCompraAction.imprimeListaReducida());

            alert.SetNegativeButton("No", (senderAlert, args) =>
            {
                dialog.Dismiss();
            });

            alert.SetPositiveButton("Sí", (senderAlert, args) =>
            {
                ListaCompraAction.firmarCompra(true);
                dialog.Dismiss();
                Intent i = new Intent(this, typeof(DialogFirmaActivity));
                //i.SetFlags(ActivityFlags.NewTask);

                StartActivityForResult(i, RESULT);
            });

            dialog = alert.Create();
            dialog.Show();
        }

        private decimal CalcularCantidad(decimal cantidad, decimal precioArticulo)
        {
            decimal retirada = cantidad / precioArticulo;

            /*if (cantidad > aprovechable) {
                new DialogConsumicion().Dialog(context, aprovechable, cantidad, cantidadAcumulada);
            } */
            if (totalConsumidoMes + retirada > _socio.ConsumicionMaxima)
            {

                if (_vuelveAMostrar)
                {
                    MuestraDialogo(cantidad, precioArticulo);
                }
                else
                {
                    Operar(cantidad, precioArticulo);
                }
            }
            else
            {
                totalARetirar = retirada;
                Operar(cantidad, precioArticulo);
            }

            return totalARetirar;
        }

        private void MuestraDialogo(decimal cantidad, decimal precioArticulo)
        {
            Dialog dialog = null;
            AlertDialog.Builder alert = new AlertDialog.Builder(this);
            alert.SetCancelable(false);
            alert.SetTitle("Atención");
            alert.SetMessage("Se supera el límite de consumo mensual. Continuar?");
            /*alert.SetPositiveButton="Delete", (senderAlert, args) => {
                Toast.MakeText(this, "Deleted!", ToastLength.Short).Show();
            });*/

            alert.SetNegativeButton("No", (senderAlert, args) =>
            {
                dialog.Dismiss();
            });

            alert.SetPositiveButton("Sí", (senderAlert, args) =>
            {
                _vuelveAMostrar = false;
                Operar(cantidad, precioArticulo);
                dialog.Dismiss();
            });

            dialog = alert.Create();
            dialog.Show();
        }

        private void Operar(decimal cantidad, decimal precioArticulo)
        {
            decimal retirada = cantidad / precioArticulo;
            aprovechable -= cantidad;
            cantidadAcumulada += cantidad;
            totalAcumulado += retirada;
            totalConsumidoMes += retirada;
            editTextParcial.Text = "\t" + Redondeo.DosDecimales(cantidadAcumulada);
            editTextTotalAConsumir.Text = "\t" + Redondeo.DosDecimales(totalAcumulado);
            editTextAprovechable.Text = "\t" + Redondeo.DosDecimales(aprovechable);
            if (totalConsumidoMes > _socio.ConsumicionMaxima)
            {
                editTextTotalConsumidoMes.Text = "\t" + Redondeo.DosDecimales(_socio.ConsumicionMaxima);
            }
            else
            {
                editTextTotalConsumidoMes.Text = "\t" + Redondeo.DosDecimales(totalConsumidoMes);
            }
        }

        private void Preguntar(int position)
        {
            Dialog dialog = null;
            AlertDialog.Builder alert = new AlertDialog.Builder(this);
            alert.SetCancelable(false);
            alert.SetTitle("Atención");
            alert.SetMessage("¿Está seguro que desea eliminar esta línea de pedido?");

            alert.SetNegativeButton("No", (senderAlert, args) =>
            {
                dialog.Dismiss();
            });

            alert.SetPositiveButton("Sí", (senderAlert, args) =>
            {
                ListaCompraAction.remove(position);

                if (ListaCompraAction.getlistArticulos() != null)
                {
                    _listViewArticulosActivo = false;
                    listViewArticulos.Adapter = null;
                    listViewArticulos.Adapter = new ListaCompraAdapter(this);
                    //myListView.setLayoutParams(new LinearLayout.LayoutParams(LinearLayout.LayoutParams.MATCH_PARENT, listArticulos.size() * 75 + 200));
                    Util.SetListViewHeightBasedOnChildren(listViewArticulos);
                }
                else
                {
                    listViewArticulos.Adapter = null;
                    listViewArticulos.Adapter = new ListViewArticuloAdapter(this, Resources, _listArticulos);
                    _listViewArticulosActivo = true;
                    Util.SetListViewHeightBasedOnChildren(listViewArticulos);
                    scrollView.ScrollTo(0, 0);
                }

                MuestraSocio();
                Toast.MakeText(this, "Se ha eliminado la línea de pedido", ToastLength.Short).Show();
                dialog.Dismiss();
            });

            dialog = alert.Create();
            dialog.Show();
        }

        private void spinner_ItemSelected(object sender, AdapterView.ItemSelectedEventArgs e)
        {
            chkMostrarListaCompra.Checked = false;

            WebServiceProductos wsProductos = new WebServiceProductos();
            /*Familia[] myArray = wsProductos.GetAllFamiliasActivas();
            _familia = null;

            _listFamilias = new List<Familia>(myArray);*/

            _familia = _listFamilias[e.Position];

            //Cargamos el listview con los productos de la familia seleccionada
            Articulo[] myArrayArticulo = wsProductos.GetAllArticulosActivosByIdFamilia(_familia.IdFamilia);
            _listArticulos = new List<Articulo>(myArrayArticulo);

            _adapterArticulos = new ListViewArticuloAdapter(this, Resources, _listArticulos);

            listViewArticulos.Adapter = _adapterArticulos;
            Util.SetListViewHeightBasedOnChildren(listViewArticulos);

            if (chkMostrarListaCompra.Checked)
            {
                if (ListaCompraAction.getlistArticulos() != null)
                {
                    _listViewArticulosActivo = false;
                    listViewArticulos.Adapter = null;
                    listViewArticulos.Adapter = new ListaCompraAdapter(this);
                }
            }
            else
            {
                _listViewArticulosActivo = true;
                listViewArticulos.Adapter = null;
                listViewArticulos.Adapter = new ListViewArticuloAdapter(this, Resources, _listArticulos);
            }
        }

        private async void buttonBuscar_Click(object sender, EventArgs e)
        {
            if (ListaCompraAction.getlistArticulos() != null)
            {
                Dialog dialog = null;
                AlertDialog.Builder alert = new AlertDialog.Builder(this);
                alert.SetCancelable(false);
                alert.SetTitle("Atención");
                alert.SetMessage("\tHay pedidos pendientes. Si continúa se perderán.\n\n\tContinuar?");

                alert.SetPositiveButton("Si", (senderAlert, args) =>
                {
                    ListaCompraAction.vaciarCarrito();
                    this.RunOnUiThread(async () =>
                    {
                        await Buscar();
                    });
                });
                alert.SetNegativeButton("No", (senderAlert, args) =>
                {
                });

                dialog = alert.Create();
                dialog.Show();
            }
            else
            {
                await Buscar();
            }
        }

        private async Task Buscar()
        {
            TecladoVirtual.Hidden(this);
            _dialog = new ProgressDialog(this);
            _dialog.SetMessage("Buscando socio. Por favor, espere...");
            _dialog.Show();

            Tuple<Socio, TablaAuxiliarSocio> tuple = await GetSocio(editTextBuscar.Text.Trim());

            _socio = tuple.Item1;
            _tablaAuxSocio = tuple.Item2;

            if (_socio != null && _tablaAuxSocio != null)
            {
                MuestraSocio();
                MuestraImagenSocio();
            }
            else
            {
                LimpiaCampos();
                Toast.MakeText(this, "No se encontró el socio", ToastLength.Long).Show();
            }

            CargarSpinner();

            editTextBuscar.Text = "";
            editTextBuscar.RequestFocus();

            _dialog.Dismiss();
        }

        private Task<Tuple<Socio, TablaAuxiliarSocio>> GetSocio(string numSoc)
        {
            return Task.Run(() =>
            {
                WebServiceSocio wsSocio = new WebServiceSocio();
                WebServiceTablaAuxiliarSocio wsTablaAuxSocio = new WebServiceTablaAuxiliarSocio();

                Socio socio = wsSocio.GetSocioByCodeOrDNI(numSoc);
                TablaAuxiliarSocio tablaAuxSocio = null;

                if (socio != null)
                {
                    tablaAuxSocio = wsTablaAuxSocio.GetTablaAuxiliarSocioByIdSocio(socio.IdSocio);
                    if (tablaAuxSocio != null)
                    {
                        _existeSocio = true;
                    }
                    else
                    {
                        _existeSocio = false;
                    }
                }
                else
                {
                    _existeSocio = false;
                }

                return Tuple.Create(socio, tablaAuxSocio);
            });
        }

        private void CargarSpinner()
        {
            WebServiceProductos wsProductos = new WebServiceProductos();
            Familia[] myArray = wsProductos.GetAllFamiliasActivas();
            _listFamilias = new List<Familia>(myArray);

            _adapterFamilias = new ListViewFamiliaAdapter(this, Resources, _listFamilias);
            spinner.Adapter = _adapterFamilias;

            CargarListViewArticulos();
        }

        private void CargarListViewArticulos()
        {
            if (_listFamilias.Count > 0)
            {
                WebServiceProductos wsProductos = new WebServiceProductos();

                Familia familia = _listFamilias[0];

                Articulo[] myArrayArticulo = wsProductos.GetAllArticulosActivosByIdFamilia(familia.IdFamilia);
                _listArticulos = new List<Articulo>(myArrayArticulo);

                _adapterArticulos = new ListViewArticuloAdapter(this, Resources, _listArticulos);

                this.RunOnUiThread(() =>
                {
                    listViewArticulos.Adapter = _adapterArticulos;
                    Util.SetListViewHeightBasedOnChildren(listViewArticulos);
                });
            }
        }

        private void LimpiaCampos()
        {
            this.RunOnUiThread(() =>
            {
                chkMostrarListaCompra.Checked = false;
                editTextAprovechable.Text = "";
                editTextLimite.Text = "";
                editTextSocio.Text = "";
                editTextParcial.Text = "";
                editTextArticulo.Text = "";
                editTextPrecio.Text = "";
                editTextTotalAConsumir.Text = "";
                editTextTotalConsumidoMes.Text = "";
                imageFoto.SetImageResource(Resource.Drawable.sin_foto_3);
            });
        }

        private void MuestraSocio()
        {
            this.RunOnUiThread(() =>
            {
                if (ListaCompraAction.getlistArticulos() != null)
                {
                    aprovechable = (decimal)ListaCompraAction.getAprovechable();

                    if (_listViewArticulosActivo == false)
                    {
                        chkMostrarListaCompra.Checked = true;
                    }
                    else
                    {
                        chkMostrarListaCompra.Checked = false;
                    }

                    cantidadAcumulada = 0;
                    //_articulo = null;
                    totalAcumulado = 0;
                    totalConsumidoMes = (decimal)ListaCompraAction.getTotalConsumidoMes();
                    editTextAprovechable.Text = "\t" + Redondeo.DosDecimales((decimal)ListaCompraAction.getAprovechable()) + " créditos.";
                    editTextLimite.Text = "\t" + _socio.ConsumicionMaxima;

                    if (totalConsumidoMes > _socio.ConsumicionMaxima)
                    {
                        editTextTotalConsumidoMes.Text = "\t" + Redondeo.DosDecimales(_socio.ConsumicionMaxima);
                    }
                    else
                    {
                        editTextTotalConsumidoMes.Text = "\t" + Redondeo.DosDecimales(totalConsumidoMes);
                    }

                    editTextSocio.Text = "\t" + Texto.GetString(_socio.Nombre) + "\n" + "\t" + Texto.GetString(_socio.PrimerApellido);
                    editTextParcial.Text = "";
                    editTextArticulo.Text = "";
                    editTextPrecio.Text = "";
                    editTextTotalAConsumir.Text = "";
                    editTextDescripcion.Text = "";

                    //MuestraImagenSocio();
                }
                else
                {
                    if (_listViewArticulosActivo == false)
                    {
                        chkMostrarListaCompra.Checked = true;
                    }
                    else
                    {
                        chkMostrarListaCompra.Checked = false;
                    }

                    //_articulo = null;
                    cantidadAcumulada = 0;
                    totalAcumulado = 0;

                    if (_socio != null && _tablaAuxSocio != null)
                    {
                        aprovechable = (decimal)_tablaAuxSocio.Aprovechable;

                        totalConsumidoMes = (decimal)_tablaAuxSocio.ConsumicionDelMes;

                        editTextAprovechable.Text = "\t" + Redondeo.DosDecimales((decimal)_tablaAuxSocio.Aprovechable) + " créditos.";
                        editTextLimite.Text = "\t" + _socio.ConsumicionMaxima;

                        if (totalConsumidoMes > _socio.ConsumicionMaxima)
                        {
                            editTextTotalConsumidoMes.Text = "\t" + Redondeo.DosDecimales(_socio.ConsumicionMaxima);
                        }
                        else
                        {
                            editTextTotalConsumidoMes.Text = "\t" + Redondeo.DosDecimales(totalConsumidoMes);
                        }

                        editTextSocio.Text = "\t" + _socio.Nombre.Trim() + "\n" + "\t" + _socio.PrimerApellido.Trim();
                    }
                    else
                    {
                        editTextAprovechable.Text = "";
                        editTextLimite.Text = "";
                        editTextTotalConsumidoMes.Text = "";
                        editTextSocio.Text = "";
                    }

                    editTextParcial.Text = "";
                    editTextArticulo.Text = "";
                    editTextPrecio.Text = "";
                    editTextTotalAConsumir.Text = "";
                    editTextDescripcion.Text = "";
                    //MuestraImagenSocio();
                }
            });
        }

        private void MuestraImagenSocio()
        {
            //Bitmap bitmap = DownloadImage.GetImage(_socio.Foto.Trim());
            Picasso.With(this).Load(UrlWebSite.GetUrlResources(Texto.GetString(_socio.Foto))).Error(Resource.Drawable.sin_foto).Into(imageFoto);

        }

        private void InitComponents()
        {
            buttonBuscar = (Button)FindViewById(Resource.Id.buttonBuscarRetirar2);
            buttonRetirar = (Button)FindViewById(Resource.Id.buttonDialogRetiradaSala2Retirar);
            editTextBuscar = (EditText)FindViewById(Resource.Id.editTextBuscarRetirar2);
            editTextDescripcion = (EditText)FindViewById(Resource.Id.editTextDialogRetirada2Descripcion);
            editTextBuscar.RequestFocus();

            listViewArticulos = (ListView)FindViewById(Resource.Id.listViewDialogConsumicionSala2);
            editTextAprovechable = (EditText)FindViewById(Resource.Id.editTextDialogConsumicionSala2Aprovechable);
            editTextSocio = (EditText)FindViewById(Resource.Id.editTextDialogConsumicionSala2Socio);
            editTextTotalConsumidoMes = (EditText)FindViewById(Resource.Id.editTextDialogConsumicionSala2Consumido);
            editTextArticulo = (EditText)FindViewById(Resource.Id.editTextRetiradaSala2NombreArticulo);
            editTextParcial = (EditText)FindViewById(Resource.Id.editTextDialogConsumicionSala2Parcial);
            editTextTotalAConsumir = (EditText)FindViewById(Resource.Id.editTextRetirada2TotalARetirar);
            editTextLimite = (EditText)FindViewById(Resource.Id.editTextDialogConsumicionSala2LimiteConsumo);
            editTextPrecio = (EditText)FindViewById(Resource.Id.editTextRetiradaSala2PrecioArticulo);
            scrollView = (ScrollView)FindViewById(Resource.Id.scrollViewRetirada2);
            spinner = (Spinner)FindViewById(Resource.Id.spinnerRetirada2);
            imageFoto = (ImageView)FindViewById(Resource.Id.imgButtonRetirada2ImageSocio);
            //imageBorrar = (ImageView) FindViewById(Resource.Id.imageViewCarritoRemove);
            chkMostrarListaCompra = (ToggleButton)FindViewById(Resource.Id.checkBoxTotalCompraRetirada2);

            buttonBorrarSoloCantidades = (Button)FindViewById(Resource.Id.buttonBorrarSoloCantidadesRetirada2);
            buttonCantidad1 = (Button)FindViewById(Resource.Id.buttonRetirada2Cantidad1);
            buttonCantidad2 = (Button)FindViewById(Resource.Id.buttonRetirada2Cantidad2);
            buttonCantidad3 = (Button)FindViewById(Resource.Id.buttonRetirada2Cantidad3);
            buttonCantidad4 = (Button)FindViewById(Resource.Id.buttonRetirada2Cantidad4);
            buttonLimpiar = (Button)FindViewById(Resource.Id.buttonDialogRetirada2Limpiar);
            buttonAnhadirACarrito = (Button)FindViewById(Resource.Id.buttonDialogRetirada2AnhadirCarrito);
        }
    }
}