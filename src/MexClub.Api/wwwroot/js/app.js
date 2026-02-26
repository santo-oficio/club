"use strict";

var MexClub = (function () {
    var _sociosPage = 1;
    var _sociosHasMore = false;
    var _retiradasPage = 1;
    var _retiradasHasMore = false;
    var _fichajeTimer = null;
    var _dashboardHoldTimer = null;
    var _dashboardHoldMs = 800;

    // ========== INIT & EVENTS ==========
    function init() {
        if (MexClubApi.loadAuth()) {
            showApp();
        } else {
            showLogin();
        }
        bindEvents();
    }

    var _pageTitles = {
        pageDashboard: "Panel Principal",
        pageSocios: "Gestión de Socios",
        pageAportaciones: "Aportaciones",
        pageRetiradas: "Retiradas / Consumiciones",
        pageCuotas: "Cuotas",
        pageArticulos: "Art\u00edculos",
        pageFamilias: "Familias"
    };

    function bindEvents() {
        $("#loginForm").on("submit", function (e) {
            e.preventDefault();
            doLogin();
        });
        $("#btnLogout").on("click", doLogout);

        // Sidebar toggle
        $("#btnToggleSidebar").on("click", toggleSidebar);
        $("#sidebarOverlay").on("click", closeSidebar);
        $("#btnBack").on("click", function () { Nav.go("pageDashboard"); });

        // Sidebar nav links
        $(".sidebar-link[data-page]").on("click", function () {
            var page = $(this).data("page");
            Nav.go(page);
            if ($(window).width() < 768) closeSidebar();
        });

        $("#searchSocios").on("input", debounce(function () {
            Socios.search();
        }, 300));

        $("#familiasToggleActivas").on("change", function () {
            Familias.load();
        });

        $("#searchFamilias").on("input", debounce(function () {
            Familias._render();
        }, 300));

        $("#articulosToggleActivos").on("change", function () {
            Articulos.load();
        });

        // Aportaciones autocomplete
        bindSocioAutocomplete("apoBuscar", "apoAutocomplete", function (socio) {
            MexClubApi.getSocio(socio.id).then(function (res) {
                if (res.success && res.data) Aportaciones._mostrarSocio(res.data);
            });
        });
        $("#btnApoBuscar").on("click", function () { Aportaciones.buscar(); });
        $("#apoCantidad").on("input", function () {
            var val = parseInt($(this).val());
            $("#btnAportar").prop("disabled", !(val > 0));
        });
        $("#btnAportar").on("click", function () { Aportaciones.aportar(); });

        // Cuotas autocomplete
        bindSocioAutocomplete("cuotaBuscar", "cuotaAutocomplete", function (socio) {
            MexClubApi.getSocio(socio.id).then(function (res) {
                if (res.success && res.data) Cuotas._mostrarSocio(res.data);
            });
        });
        $("#btnCuotaBuscar").on("click", function () { Cuotas.buscar(); });
        $("#btnCuotaAnual").on("click", function () { Cuotas.pagarAnual(); });
        $("#btnCuotaMensual").on("click", function () { Cuotas.pagarMensual(); });
        $("#filterFamiliaArticulos").on("change", function () {
            Articulos.load();
        });
        $("#searchArticulos").on("input", debounce(function () {
            Articulos.load();
        }, 300));

        $("#fichajeCodigo").on("keyup", function (e) {
            if (e.key === "Enter") MexClub.Fichaje.fichar();
        });

        // controles numéricos +/-
        $(document).on("click", ".btn-number", function (e) {
            e.preventDefault();
            var dir = $(this).data("dir");
            var target = $(this).data("target");
            var step = parseFloat($(this).data("step")) || 1;
            var $input = $("#" + target);
            var val = parseFloat($input.val());
            if (isNaN(val)) val = 0;
            var next = dir === "+" ? val + step : val - step;
            var min = $input.attr("min");
            if (min !== undefined && min !== null && min !== "" && next < parseFloat(min)) next = parseFloat(min);
            $input.val(next).trigger("input");
        });

        $(document).on("input", ".number-input", function () {
            var val = $(this).val();
            // permitir escritura libre, validación será al enviar
            if (val === "") return;
            var num = parseFloat(val);
            if (isNaN(num)) $(this).val(0);
        });

        $(document).on("click", "[data-action]", function () {
            var $el = $(this);
            var action = $el.data("action");
            var id = $el.data("id");

            switch (action) {
                case "socio-detail": Socios.showDetail(id); break;
                case "socio-edit": Socios.showEdit(id); break;
                case "socio-do-edit": Socios.doEdit(); break;
                case "socio-create": Socios.showCreate(); break;
                case "socio-do-create": Socios.doCreate(); break;
                case "aportacion-delete": Aportaciones.confirmDelete(id); break;
                case "retirada-create": Retiradas.showCreate(); break;
                case "retirada-load-more": Retiradas.loadMore(); break;
                case "articulo-detail": Articulos.showDetail(id); break;
                case "articulo-edit": Articulos.showEdit(id); break;
                case "articulo-do-edit": Articulos.doEdit(); break;
                case "articulo-create": Articulos.showCreate(); break;
                case "articulo-do-create": Articulos.doCreate(); break;
                case "familia-create": Familias.showCreate(); break;
                case "familia-do-create": Familias.doCreate(); break;
                case "familia-edit": Familias.showEdit(id); break;
                case "familia-do-edit": Familias.doEdit(); break;
            }
        });

        bindDashboardLongPressEvents();
    }

    function bindDashboardLongPressEvents() {
        function clearTimer() {
            if (_dashboardHoldTimer) {
                clearTimeout(_dashboardHoldTimer);
                _dashboardHoldTimer = null;
            }
        }

        $(document).on("contextmenu", ".js-dashboard-delete-item", function (e) {
            e.preventDefault();
        });

        $(document).on("mousedown touchstart", ".js-dashboard-delete-item", function () {
            var $item = $(this);
            clearTimer();
            _dashboardHoldTimer = setTimeout(function () {
                $item.data("hold-fired", true);
                showDashboardDeleteConfirm($item.data("kind"), parseInt($item.data("id"), 10));
            }, _dashboardHoldMs);
        });

        $(document).on("mouseup mouseleave touchend touchcancel", ".js-dashboard-delete-item", function () {
            clearTimer();
        });

        $(document).on("click", ".js-dashboard-delete-item", function (e) {
            if ($(this).data("hold-fired")) {
                e.preventDefault();
                e.stopImmediatePropagation();
                $(this).data("hold-fired", false);
            }
        });
    }

    function showDashboardDeleteConfirm(kind, id) {
        if (!(id > 0)) return;

        var isAportacion = kind === "aportacion";
        var title = isAportacion ? "Eliminar aportación" : "Eliminar retirada";
        var body = isAportacion
            ? '<p class="mb-0">¿Quieres borrar esta aportación? Se descontará del disponible del socio.</p>'
            : '<p class="mb-0">¿Quieres borrar esta retirada? Se recalcularán disponible y consumido del socio.</p>';
        var footer = '<button class="btn btn-outline-secondary" data-bs-dismiss="modal">Cancelar</button>'
            + '<button class="btn btn-danger" id="btnConfirmDashboardDelete">Sí, borrar</button>';

        showModal(title, body, footer);

        $(document).off("click.dashboard-delete", "#btnConfirmDashboardDelete").on("click.dashboard-delete", "#btnConfirmDashboardDelete", function () {
            var req = isAportacion ? MexClubApi.deleteAportacion(id) : MexClubApi.deleteRetirada(id);
            req.then(function (res) {
                if (res && res.success) {
                    closeModal();
                    showToast("OK", isAportacion ? "Aportación eliminada" : "Retirada eliminada", "success");
                    loadDashboard();
                } else {
                    showToast("Error", (res && res.message) ? res.message : "No se pudo eliminar", "danger");
                }
            }).catch(function (err) {
                showToast("Error", err && err.message ? err.message : "No se pudo eliminar", "danger");
            });
        });
    }

    function toggleSidebar() {
        $("#sidebar").toggleClass("open");
        $("#sidebarOverlay").toggleClass("show");
    }
    function closeSidebar() {
        $("#sidebar").removeClass("open");
        $("#sidebarOverlay").removeClass("show");
    }

    // ========== AUTH SCREENS ==========
    function showLogin() {
        $("#loginScreen").addClass("active");
        $("#appScreen").removeClass("active");
        $("#loginUsername").val("").focus();
        $("#loginPassword").val("");
        $("#loginError").addClass("d-none");
    }

    function showApp() {
        var user = MexClubApi.getUser();
        if (!user) { showLogin(); return; }

        $("#loginScreen").removeClass("active");
        $("#appScreen").addClass("active");
        $("#navUser").text(user.username);
        $("#sidebarUser").text(user.username);
        $("body").toggleClass("role-socio", !MexClubApi.isAdmin());

        loadDashboard();
    }

    function doLogin() {
        var u = $("#loginUsername").val().trim();
        var p = $("#loginPassword").val();
        if (!u || !p) return;

        $("#loginSpinner").removeClass("d-none");
        $("#btnLogin").prop("disabled", true);
        $("#loginError").addClass("d-none");

        MexClubApi.login(u, p)
            .then(function (res) {
                if (res.success) {
                    showApp();
                } else {
                    showLoginError((res.errors && res.errors[0]) || res.message || "Error de autenticación");
                }
            })
            .catch(function (err) {
                showLoginError(err.message || "Error de conexión con el servidor");
            })
            .always(function () {
                $("#loginSpinner").addClass("d-none");
                $("#btnLogin").prop("disabled", false);
            });
    }

    function showLoginError(msg) {
        $("#loginError").text(msg).removeClass("d-none");
    }

    function doLogout() {
        MexClubApi.logout();
        showLogin();
    }

    // ========== DASHBOARD ==========
    function loadDashboard() {
        MexClubApi.getDashboard().then(function (res) {
            if (!res.success) return;
            var d = res.data;
            $("#statSocios").text(d.totalSociosActivos);
            $("#statDentro").text(d.sociosDentro || 0);
            $("#statAportaciones").text(formatCurrency(d.totalAportaciones));
            $("#statRetiradas").text(formatCurrency(d.totalRetiradas));

            var aportaciones = d.ultimasAportaciones || [];
            $("#listUltimasAportaciones").html(
                aportaciones.length ? aportaciones.map(renderAportacionItem).join("") : emptyState("Sin aportaciones recientes", "piggy-bank")
            );

            var retiradas = d.ultimasRetiradas || [];
            $("#listUltimasRetiradas").html(
                retiradas.length ? retiradas.map(renderRetiradaItem).join("") : emptyState("Sin retiradas recientes", "cart-dash")
            );
        }).catch(noop);
    }

    // ========== UI HELPERS ==========
    function showToast(title, message, type) {
        var $t = $("#appToast");
        $t.removeClass("bg-success bg-danger bg-warning bg-info text-white");
        if (type) $t.addClass("bg-" + type + " text-white");
        $("#toastTitle").text(title);
        $("#toastBody").text(message);
        new bootstrap.Toast($t[0], { delay: 3000 }).show();
    }

    var _modalInstance = null;

    function showModal(title, bodyHtml, footerHtml) {
        var el = document.getElementById("genericModal");
        if (_modalInstance) {
            _modalInstance.dispose();
            _modalInstance = null;
        }
        $("#modalTitle").text(title);
        $("#modalBody").html(bodyHtml);
        $("#modalFooter").html(footerHtml || "");
        _modalInstance = new bootstrap.Modal(el);
        _modalInstance.show();
    }

    function closeModal() {
        if (_modalInstance) {
            _modalInstance.hide();
        }
        // cleanup stale backdrops
        $(".modal-backdrop").remove();
        $("body").removeClass("modal-open").css({ overflow: "", paddingRight: "" });
    }

    function escapeHtml(str) {
        if (!str) return "";
        var div = document.createElement("div");
        div.appendChild(document.createTextNode(String(str).trim()));
        return div.innerHTML;
    }

    function formatDateTime(dateStr) {
        if (!dateStr) return "";
        var d = new Date(dateStr);
        return d.toLocaleDateString("es-ES") + " " + d.toLocaleTimeString("es-ES", { hour: "2-digit", minute: "2-digit" });
    }

    function formatDate(dateStr) {
        if (!dateStr) return "";
        return new Date(dateStr).toLocaleDateString("es-ES");
    }

    function formatCurrency(val) {
        return parseFloat(val || 0).toFixed(2) + " \u20AC";
    }

    function emptyState(msg, icon) {
        var ico = icon || "inbox";
        return '<div class="empty-placeholder"><i class="bi bi-' + ico + '"></i><span>' + escapeHtml(msg) + '</span></div>';
    }

    function noop() {}

    function debounce(fn, delay) {
        var timer;
        return function () {
            var ctx = this, args = arguments;
            clearTimeout(timer);
            timer = setTimeout(function () { fn.apply(ctx, args); }, delay);
        };
    }

    // ========== RENDER HELPERS ==========
    // Fallback foto
    var DEFAULT_PHOTO = "/images/sin_foto.jpg";

    // Deterministic color per letter (never white/very light)
    var _letterColors = {};
    var _colorPalette = [
        "#e53935","#d81b60","#8e24aa","#5e35b1","#3949ab","#1e88e5","#039be5","#00acc1",
        "#00897b","#43a047","#7cb342","#c0ca33","#f9a825","#ffb300","#fb8c00","#f4511e",
        "#6d4c41","#546e7a","#5c6bc0","#26a69a","#ec407a","#ab47bc","#7e57c2","#42a5f5",
        "#29b6f6","#26c6da","#66bb6a","#9ccc65","#d4e157","#fdd835","#ffa726","#ff7043"
    ];
    function letterColor(ch) {
        var c = (ch || "?").toUpperCase();
        if (!_letterColors[c]) {
            var idx = c.charCodeAt(0) % _colorPalette.length;
            _letterColors[c] = _colorPalette[idx];
        }
        return _letterColors[c];
    }
    function letterAvatar(name, size) {
        var s = size || 40;
        var ch = (name || "?").charAt(0).toUpperCase();
        var bg = letterColor(ch);
        return '<span style="display:inline-flex;align-items:center;justify-content:center;width:' + s + 'px;height:' + s + 'px;border-radius:50%;background:' + bg + ';color:#fff;font-weight:600;font-size:' + Math.round(s * 0.45) + 'px;flex-shrink:0">' + escapeHtml(ch) + '</span>';
    }

    function renderAccesoItem(a) {
        var badgeClass = a.tipoAcceso === "Entrada" ? "badge-entrada" : "badge-salida";
        return '<div class="list-group-item d-flex justify-content-between align-items-center">'
            + '<div><div class="fw-semibold">' + escapeHtml(a.socioNombre) + '</div>'
            + '<small class="text-muted">' + formatDateTime(a.fechaHora) + '</small></div>'
            + '<span class="badge ' + badgeClass + '">' + escapeHtml(a.tipoAcceso) + '</span></div>';
    }

    function renderSocioItem(s) {
        var avatar = renderSocioAvatar(s.nombreCompleto, s.fotoUrl);
        var d = s.detalle || {};
        var cuota = d.debeCuota ? '<br><small class="text-danger fw-semibold"><i class="bi bi-exclamation-triangle me-1"></i>Cuota pendiente</small>' : '';
        var fechaSocio = s.fechaAlta ? ' • <i class="bi bi-calendar-check me-1"></i>Socio desde ' + formatDate(s.fechaAlta) : '';
        return '<a class="list-group-item list-group-item-action" data-action="socio-detail" data-id="' + s.id + '">'
            + '<div class="d-flex align-items-center gap-2">'
            + avatar
            + '<div class="flex-grow-1 min-width-0"><div class="fw-semibold text-truncate">' + escapeHtml(s.nombreCompleto) + '</div>'
            + '<small class="text-muted"><i class="bi bi-credit-card me-1"></i>' + escapeHtml(s.documento) + fechaSocio + '</small>'
            + cuota + '</div>'
            + '<i class="bi bi-chevron-right text-muted"></i></div></a>';
    }

    function renderSocioAvatar(name, fotoUrl, large) {
        var cls = large ? "socio-avatar-lg" : "socio-avatar";
        var src = (fotoUrl && fotoUrl.charAt(0) === "/") ? escapeHtml(fotoUrl) : DEFAULT_PHOTO;
        return '<img class="' + cls + '" src="' + src + '" alt="" onerror="this.onerror=null;this.src=\'/images/sin_foto.jpg\'">';
    }

    // ========== SOCIO AUTOCOMPLETE (shared by Aportaciones & Cuotas) ==========
    function bindSocioAutocomplete(inputId, listId, onSelect) {
        var $input = $("#" + inputId);
        var $list = $("#" + listId);
        var _acItems = [];
        var _acIdx = -1;

        function acAvatar(socio) {
            var src = (socio.fotoUrl && socio.fotoUrl.charAt(0) === "/") ? socio.fotoUrl : DEFAULT_PHOTO;
            return '<img class="ac-avatar" src="' + escapeHtml(src) + '" alt="" onerror="this.onerror=null;this.src=\'/images/sin_foto.jpg\'">';
        }

        function showResults(items) {
            _acItems = items;
            _acIdx = -1;
            if (!items.length) {
                $list.html('<div class="socio-ac-empty">Sin resultados</div>').removeClass("d-none");
                return;
            }
            var html = items.map(function (s, i) {
                var meta = [];
                if (s.codigo) meta.push(escapeHtml(s.codigo));
                if (s.documento) meta.push(escapeHtml(s.documento));
                return '<div class="socio-ac-item" data-ac-idx="' + i + '">'
                    + acAvatar(s)
                    + '<div class="ac-info">'
                    + '<div class="ac-name">' + escapeHtml(s.nombreCompleto) + '</div>'
                    + '<div class="ac-meta">' + meta.join(" &bull; ") + '</div>'
                    + '</div></div>';
            }).join("");
            $list.html(html).removeClass("d-none");
        }

        function hide() { $list.addClass("d-none").html(""); _acItems = []; _acIdx = -1; }

        function selectIdx(idx) {
            if (idx >= 0 && idx < _acItems.length) {
                var socio = _acItems[idx];
                $input.val(socio.nombreCompleto);
                hide();
                onSelect(socio);
            }
        }

        var doSearch = debounce(function () {
            var q = ($input.val() || "").trim();
            if (q.length < 2) { hide(); return; }
            MexClubApi.searchSocios(q, 10)
                .then(function (res) {
                    if (!res.success) { hide(); return; }
                    showResults(res.data || []);
                })
                .catch(function () { hide(); });
        }, 250);

        $input.on("input", doSearch);

        $input.on("keydown", function (e) {
            if ($list.hasClass("d-none") || !_acItems.length) {
                if (e.key === "Enter") {
                    e.preventDefault();
                    // Direct search if no dropdown
                    var q = ($input.val() || "").trim();
                    if (q.length >= 2) {
                        MexClubApi.searchSocios(q, 1).then(function (res) {
                            if (res.success && res.data && res.data.length) {
                                $input.val(res.data[0].nombreCompleto);
                                onSelect(res.data[0]);
                            }
                        });
                    }
                }
                return;
            }
            if (e.key === "ArrowDown") {
                e.preventDefault();
                _acIdx = Math.min(_acIdx + 1, _acItems.length - 1);
                $list.find(".socio-ac-item").removeClass("active").eq(_acIdx).addClass("active");
            } else if (e.key === "ArrowUp") {
                e.preventDefault();
                _acIdx = Math.max(_acIdx - 1, 0);
                $list.find(".socio-ac-item").removeClass("active").eq(_acIdx).addClass("active");
            } else if (e.key === "Enter") {
                e.preventDefault();
                selectIdx(_acIdx >= 0 ? _acIdx : 0);
            } else if (e.key === "Escape") {
                hide();
            }
        });

        $list.on("click", ".socio-ac-item", function () {
            selectIdx(parseInt($(this).data("ac-idx")));
        });

        // Close on outside click
        $(document).on("mousedown", function (e) {
            if (!$(e.target).closest($list).length && !$(e.target).closest($input).length) {
                hide();
            }
        });
    }

    function renderAportacionItem(a) {
        return '<button type="button" class="list-group-item list-group-item-action js-dashboard-delete-item" data-kind="aportacion" data-id="' + a.id + '">'
            + '<div class="d-flex align-items-center gap-2">'
            + renderSocioAvatar(a.socioNombre, a.socioFotoUrl)
            + '<div class="flex-grow-1 min-width-0">'
            + '<div class="fw-semibold text-truncate">' + escapeHtml(a.socioNombre) + '</div>'
            + '<small class="text-muted">' + escapeHtml(a.socioDocumento) + ' &bull; ' + formatDateTime(a.fecha) + '</small>'
            + '</div>'
            + '<span class="fw-bold text-success text-nowrap">+' + formatCurrency(a.cantidadAportada) + '</span>'
            + '</div></button>';
    }

    function renderRetiradaItem(r) {
        return '<button type="button" class="list-group-item list-group-item-action js-dashboard-delete-item" data-kind="retirada" data-id="' + r.id + '">'
            + '<div class="d-flex align-items-center gap-2">'
            + renderSocioAvatar(r.socioNombre, r.socioFotoUrl)
            + '<div class="flex-grow-1 min-width-0">'
            + '<div class="fw-semibold text-truncate">' + escapeHtml(r.socioNombre) + '</div>'
            + '<small class="text-muted">' + escapeHtml(r.articuloNombre) + ' x' + r.cantidad + '</small>'
            + '<br><small class="text-muted">' + formatDateTime(r.fecha) + '</small>'
            + '</div>'
            + '<span class="fw-bold text-danger text-nowrap">-' + formatCurrency(r.total) + '</span>'
            + '</div></button>';
    }

    function formInput(label, id, type, required, icon) {
        type = type || "text";
        var req = required ? ' required' : '';
        var ico = icon ? '<i class="bi bi-' + icon + ' me-1"></i>' : '';
        var reqStar = required ? ' <span class="text-danger">*</span>' : '';
        if (type === "number") {
            return '<div class="mb-3"><label class="form-label" for="' + id + '">' + ico + label + reqStar + '</label>'
                + '<div class="number-control" data-target="' + id + '">' 
                + '<button class="btn btn-outline-secondary btn-number" type="button" data-dir="-" data-target="' + id + '" data-step="1">-</button>'
                + '<input type="number" class="form-control number-input" id="' + id + '"' + req + ' step="1" min="0">'
                + '<button class="btn btn-outline-secondary btn-number" type="button" data-dir="+" data-target="' + id + '" data-step="1">+</button>'
                + '</div></div>';
        }
        return '<div class="mb-3"><label class="form-label" for="' + id + '">' + ico + label + reqStar + '</label>'
            + '<input type="' + type + '" class="form-control" id="' + id + '"' + req + '></div>';
    }

    function formTextarea(label, id, rows, icon) {
        var ico = icon ? '<i class="bi bi-' + icon + ' me-1"></i>' : '';
        return '<div class="mb-3"><label class="form-label" for="' + id + '">' + ico + label + '</label>'
            + '<textarea class="form-control" id="' + id + '" rows="' + (rows || 3) + '"></textarea></div>';
    }

    function formFileInput(label, id, previewId, icon, placeholderImg, required, defaultImg) {
        var ico = icon ? '<i class="bi bi-' + icon + ' me-1"></i>' : '';
        var fallback = defaultImg || '/images/sin_foto.jpg';
        var src = placeholderImg || fallback;
        var reqCls = required ? ' required-field' : '';
        var reqStar = required ? ' <span class="text-danger">*</span>' : '';
        return '<div class="mb-3"><label class="form-label' + reqCls + '" for="' + id + '">' + ico + label + reqStar + '</label>'
            + '<div class="text-center mb-2"><img id="' + previewId + '" src="' + src + '" class="img-thumbnail" style="max-height:140px;cursor:pointer" onclick="document.getElementById(\'' + id + '\').click()" onerror="this.onerror=null;this.src=\'' + fallback + '\'"></div>'
            + '<input type="file" class="form-control d-none" id="' + id + '" accept="image/*" capture="environment"></div>';
    }

    function formSelect(label, id, options, icon) {
        var ico = icon ? '<i class="bi bi-' + icon + ' me-1"></i>' : '';
        var opts = options.map(function (o) {
            return '<option value="' + o.value + '">' + escapeHtml(o.text) + '</option>';
        }).join("");
        return '<div class="mb-3"><label class="form-label" for="' + id + '">' + ico + label + '</label>'
            + '<select class="form-select" id="' + id + '">' + opts + '</select></div>';
    }

    function infoRow(label, value, icon) {
        var ico = icon ? '<i class="bi bi-' + icon + ' me-1 text-muted"></i>' : '';
        return '<div class="col-6 mb-2"><small class="text-muted d-block">' + ico + label + '</small>'
            + '<span class="fw-semibold">' + escapeHtml(value || "-") + '</span></div>';
    }

    function socioLookupWidget(inputId, infoId) {
        return '<div class="mb-3"><label class="form-label" for="' + inputId + '"><i class="bi bi-person-badge me-1"></i>Código Socio</label>'
            + '<div class="input-group"><input type="text" class="form-control" id="' + inputId + '" placeholder="Código o documento" required>'
            + '<button class="btn btn-outline-secondary" type="button" id="' + inputId + 'Btn"><i class="bi bi-search"></i></button></div></div>'
            + '<div id="' + infoId + '" class="socio-lookup-card d-none mb-3"></div>';
    }

    function bindSocioLookup(inputId, infoId) {
        function doLookup() {
            var codigo = $("#" + inputId).val().trim();
            if (!codigo) { $("#" + infoId).addClass("d-none"); return; }
            MexClubApi.buscarSocio(codigo).then(function (r) {
                if (r.success && r.data) {
                    var s = r.data;
                    var d = s.detalle || {};
                    var avatar = renderSocioAvatar(s.nombreCompleto, s.fotoUrl, true);
                    $("#" + infoId).removeClass("d-none").html(
                        '<div class="d-flex align-items-center gap-3">'
                        + avatar
                        + '<div class="flex-grow-1">'
                        + '<div class="fw-bold">' + escapeHtml(s.nombreCompleto) + '</div>'
                        + '<small class="text-muted"><i class="bi bi-hash"></i>' + s.numSocio
                        + ' &bull; <i class="bi bi-credit-card me-1"></i>' + escapeHtml(s.documento) + '</small>'
                        + '<div class="mt-1"><span class="badge bg-success"><i class="bi bi-wallet2 me-1"></i>' + formatCurrency(d.aprovechable) + '</span>'
                        + ' <span class="badge bg-secondary"><i class="bi bi-graph-down me-1"></i>Mes: ' + formatCurrency(d.consumicionDelMes) + '</span></div>'
                        + '</div></div>'
                    );
                    $("#" + infoId).data("socio-id", s.id);
                } else {
                    $("#" + infoId).removeClass("d-none").html(
                        '<div class="text-danger"><i class="bi bi-exclamation-triangle me-1"></i>Socio no encontrado</div>'
                    );
                    $("#" + infoId).removeData("socio-id");
                }
            }).catch(noop);
        }
        $("#" + inputId).on("blur", doLookup);
        $("#" + inputId + "Btn").on("click", doLookup);
        $("#" + inputId).on("keyup", function (e) { if (e.key === "Enter") { e.preventDefault(); doLookup(); } });
    }

    function filePreview(fileInputId, previewId) {
        $("#" + fileInputId).on("change", function () {
            var file = this.files[0];
            if (!file) return;
            var reader = new FileReader();
            reader.onload = function (e) {
                $("#" + previewId).attr("src", e.target.result);
            };
            reader.readAsDataURL(file);
        });
    }

    function getFormVal(id) {
        return $("#" + id).val();
    }

    function getFormValTrimmed(id) {
        return ($("#" + id).val() || "").trim();
    }

    function getFormValOrNull(id) {
        var v = getFormValTrimmed(id);
        return v || null;
    }

    function getFormFloat(id) {
        return parseFloat(getFormVal(id)) || 0;
    }

    function getFormInt(id) {
        return parseInt(getFormVal(id), 10) || 0;
    }

    // ========== NAVIGATION ==========
    var Nav = {
        go: function (pageId) {
            $(".page").removeClass("active");
            $("#" + pageId).addClass("active");
            $(".sidebar-link").removeClass("active");
            $(".sidebar-link[data-page='" + pageId + "']").addClass("active");
            $("#navTitle").text(_pageTitles[pageId] || "MexClub");
            $("#btnBack").toggleClass("d-none", pageId === "pageDashboard");

            var loaders = {
                pageDashboard: loadDashboard,
                pageSocios: function () { Socios.load(); },
                pageArticulos: function () { Articulos.load(); },
                pageFamilias: function () { Familias.load(); },
                pageAportaciones: function () { Aportaciones.load(); },
                pageRetiradas: function () { Retiradas.load(); },
                pageCuotas: function () { Cuotas.load(); }
            };
            if (loaders[pageId]) loaders[pageId]();
        }
    };

    // ========== SOCIO FORM HELPERS ==========
    function socioFormHtml(s) {
        s = s || {};
        return '<form id="formSocio">'
            + '<h6 class="text-muted mb-3"><i class="bi bi-person-vcard me-1"></i>Datos personales</h6>'
            + formInput("Código", "socCodigo", "text", true, "qr-code")
            + '<div class="row"><div class="col-6">' + formInput("Nombre", "socNombre", "text", true, "person") + '</div>'
            + '<div class="col-6">' + formInput("Primer Apellido", "socApellido1", "text", true) + '</div></div>'
            + formInput("Segundo Apellido", "socApellido2")
            + '<div class="row"><div class="col-5">'
            + formSelect("Tipo Doc.", "socTipoDoc", [
                { value: "DNI", text: "DNI" },
                { value: "NIE", text: "NIE" },
                { value: "Pasaporte", text: "Pasaporte" }
            ], "card-list")
            + '</div><div class="col-7">' + formInput("Documento", "socDocumento", "text", true, "credit-card") + '</div></div>'
            + formInput("F. Nacimiento", "socFechaNac", "date", true, "calendar-heart")
            + '<hr><h6 class="text-muted mb-3"><i class="bi bi-geo-alt me-1"></i>Contacto y dirección</h6>'
            + '<div class="row"><div class="col-6">' + formInput("Email", "socEmail", "email", false, "envelope") + '</div>'
            + '<div class="col-6">' + formInput("Teléfono", "socTelefono", "tel", false, "telephone") + '</div></div>'
            + formInput("Dirección", "socDireccion", "text", true, "geo-alt")
            + '<div class="row"><div class="col-6">' + formInput("Localidad", "socLocalidad") + '</div>'
            + '<div class="col-6">' + formInput("Provincia", "socProvincia") + '</div></div>'
            + '<div class="row"><div class="col-6">' + formInput("País", "socPais", "text", true) + '</div>'
            + '<div class="col-6">' + formInput("Código Postal", "socCP", "text", true) + '</div></div>'
            + '<hr><h6 class="text-muted mb-3"><i class="bi bi-sliders me-1"></i>Configuración</h6>'
            + formInput("Referido Por (código)", "socReferido", "text", true, "people")
            + formInput("Consumición Máx. Mensual", "socConsMax", "number", false, "speedometer")
            + formTextarea("Comentario", "socComentario", 3, "chat-left-text")
            + '<hr><h6 class="text-muted mb-3"><i class="bi bi-camera me-1"></i>Imágenes</h6>'
            + formFileInput("Foto del socio", "socFoto", "socFotoPreview", "person-bounding-box", s.fotoUrl, true, "/images/sin_foto.jpg")
            + '<div class="row"><div class="col-6">'
            + formFileInput("DNI Anverso", "socDniAnverso", "socDniAnvPreview", "credit-card-2-front", s.fotoAnversoDniUrl, true, "/images/dni_anverso.jpg")
            + '</div><div class="col-6">'
            + formFileInput("DNI Reverso", "socDniReverso", "socDniRevPreview", "credit-card-2-back", s.fotoReversoDniUrl, true, "/images/dni_reverso.jpg")
            + '</div></div>'
            + '</form>';
    }

    function bindSocioFormFiles() {
        filePreview("socFoto", "socFotoPreview");
        filePreview("socDniAnverso", "socDniAnvPreview");
        filePreview("socDniReverso", "socDniRevPreview");
    }

    function fillSocioForm(s) {
        $("#socCodigo").val(s.codigo);
        $("#socNombre").val(s.nombre);
        $("#socApellido1").val(s.primerApellido);
        $("#socApellido2").val(s.segundoApellido || "");
        $("#socTipoDoc").val(s.tipoDocumento);
        $("#socDocumento").val(s.documento);
        $("#socFechaNac").val(s.fechaNacimiento ? s.fechaNacimiento.substring(0, 10) : "");
        $("#socEmail").val(s.email || "");
        $("#socTelefono").val(s.telefono || "");
        $("#socDireccion").val(s.direccion || "");
        $("#socLocalidad").val(s.localidad || "");
        $("#socProvincia").val(s.provincia || "");
        $("#socPais").val(s.pais || "");
        $("#socCP").val(s.codigoPostal || "");
        $("#socReferido").val(s.referidoPorCodigo || "");
        $("#socConsMax").val(s.consumicionMaximaMensual || 60);
        $("#socComentario").val(s.comentario || "");
    }

    function validateSocioForm(isEdit) {
        var fields = [
            { id: "socCodigo", label: "Código" },
            { id: "socNombre", label: "Nombre" },
            { id: "socApellido1", label: "Primer Apellido" },
            { id: "socDocumento", label: "Documento" },
            { id: "socFechaNac", label: "Fecha Nacimiento" },
            { id: "socDireccion", label: "Dirección" },
            { id: "socPais", label: "País" },
            { id: "socCP", label: "Código Postal" },
            { id: "socReferido", label: "Referido Por" }
        ];
        var valid = true;
        $("#formSocio .is-invalid").removeClass("is-invalid");
        for (var i = 0; i < fields.length; i++) {
            var $el = $("#" + fields[i].id);
            if (!$el.val() || !$el.val().trim()) {
                $el.addClass("is-invalid");
                valid = false;
            }
        }
        if (!isEdit) {
            var fileIds = ["socFoto", "socDniAnverso", "socDniReverso"];
            for (var j = 0; j < fileIds.length; j++) {
                var el = document.getElementById(fileIds[j]);
                if (!el || !el.files || !el.files.length) {
                    $("#" + fileIds[j] + "Preview").addClass("border-danger");
                    valid = false;
                } else {
                    $("#" + fileIds[j] + "Preview").removeClass("border-danger");
                }
            }
        }
        if (!valid) {
            showToast("Campos obligatorios", "Rellena todos los campos marcados con *", "warning");
        }
        return valid;
    }

    function collectSocioData() {
        return {
            codigo: getFormVal("socCodigo"),
            nombre: getFormVal("socNombre"),
            primerApellido: getFormVal("socApellido1"),
            segundoApellido: getFormValOrNull("socApellido2"),
            tipoDocumento: getFormVal("socTipoDoc"),
            documento: getFormVal("socDocumento"),
            email: getFormValOrNull("socEmail"),
            telefono: getFormValOrNull("socTelefono"),
            direccion: getFormValOrNull("socDireccion"),
            localidad: getFormValOrNull("socLocalidad"),
            provincia: getFormValOrNull("socProvincia"),
            pais: getFormValOrNull("socPais"),
            codigoPostal: getFormValOrNull("socCP"),
            fechaNacimiento: getFormValOrNull("socFechaNac"),
            referidoPor: getFormValOrNull("socReferido"),
            estrellas: 0,
            consumicionMaximaMensual: getFormInt("socConsMax"),
            esTerapeutica: false,
            esExento: false,
            pagoConTarjeta: false,
            comentario: getFormValOrNull("socComentario")
        };
    }

    function compressImage(file, maxWidth, quality) {
        return new Promise(function (resolve, reject) {
            if (!file || !file.type.match(/image.*/)) {
                resolve({ blob: file, name: file ? file.name : "" });
                return;
            }

            var reader = new FileReader();
            reader.onload = function (e) {
                var img = new Image();
                img.onload = function () {
                    var width = img.width;
                    var height = img.height;

                    if (width > maxWidth) {
                        height = Math.round(height * (maxWidth / width));
                        width = maxWidth;
                    }

                    var canvas = document.createElement('canvas');
                    canvas.width = width;
                    canvas.height = height;
                    var ctx = canvas.getContext('2d');
                    ctx.drawImage(img, 0, 0, width, height);

                    canvas.toBlob(function (blob) {
                        if (blob) {
                            resolve({ blob: blob, name: file.name });
                        } else {
                            resolve({ blob: file, name: file.name });
                        }
                    }, 'image/jpeg', quality);
                };
                img.onerror = function () { resolve({ blob: file, name: file.name }); };
                img.src = e.target.result;
            };
            reader.onerror = function () { resolve({ blob: file, name: file.name }); };
            reader.readAsDataURL(file);
        });
    }

    function uploadSocioFiles(socioId) {
        var fotoFile = document.getElementById("socFoto").files[0];
        var anvFile = document.getElementById("socDniAnverso").files[0];
        var revFile = document.getElementById("socDniReverso").files[0];

        if (!fotoFile && !anvFile && !revFile) {
            return $.Deferred().resolve().promise();
        }

        var tasks = [];
        function addTask(key, file) {
            return compressImage(file, 1280, 0.7).then(function (res) {
                return { key: key, val: res };
            });
        }

        if (fotoFile) tasks.push(addTask("foto", fotoFile));
        if (anvFile) tasks.push(addTask("dniAnverso", anvFile));
        if (revFile) tasks.push(addTask("dniReverso", revFile));

        return Promise.all(tasks).then(function (results) {
            var fd = new FormData();
            results.forEach(function (item) {
                fd.append(item.key, item.val.blob, item.val.name);
            });
            return MexClubApi.uploadSocioImages(socioId, fd);
        });
    }

    // ========== SOCIOS ==========
    var _editSocioId = null;

    var Socios = {
        load: function (page) {
            _sociosPage = page || 1;
            // User requested NOT to show list immediately.
            if (_sociosPage === 1) {
                $("#listSocios").html(
                    '<div class="text-center py-5 text-muted">'
                    + '<i class="bi bi-search" style="font-size: 3rem;"></i>'
                    + '<p class="mt-3">Utilice el buscador para encontrar socios</p>'
                    + '</div>'
                );
                $("#btnLoadMoreSocios").addClass("d-none");
                return;
            }
            // Logic for loadMore if needed, or if we decide to allow "Show All" later
        },

        loadMore: function () {
            // Only if we are in a search context or browsing all
            // For now, simpler to just keep search-based.
        },

        search: function () {
            var q = getFormValTrimmed("searchSocios");
            if (!q) { Socios.load(); return; }

            MexClubApi.searchSocios(q, 30)
                .then(function (res) {
                    if (!res.success) return;
                    var items = res.data || [];
                    $("#listSocios").html(
                        items.length ? items.map(renderSocioItem).join("") : emptyState("No se encontraron socios")
                    );
                    $("#btnLoadMoreSocios").addClass("d-none");
                })
                .catch(function () {
                    $("#listSocios").html(emptyState("Error al buscar"));
                });
        },

        showDetail: function (id) {
            MexClubApi.getSocio(id)
                .then(function (res) {
                    if (!res.success) return;
                    var s = res.data;
                    var d = s.detalle || {};
                    var avatar = renderSocioAvatar(s.nombreCompleto, s.fotoUrl, true);
                    var body = '<div class="text-center mb-3">'
                        + '<div class="d-flex justify-content-center mb-2">' + avatar + '</div>'
                        + '<h5 class="mb-1">' + escapeHtml(s.nombreCompleto) + '</h5>'
                        + '<span class="badge bg-secondary"><i class="bi bi-qr-code me-1"></i>' + escapeHtml(s.codigo) + '</span>'
                        + '</div>'
                        + '<div class="row g-2">'
                        + infoRow("Documento", s.tipoDocumento + ": " + s.documento, "credit-card")
                        + infoRow("Email", s.email, "envelope")
                        + '<div class="col-12 mb-2"><small class="text-muted d-block"><i class="bi bi-geo-alt me-1 text-muted"></i>Dirección</small><span class="fw-semibold">' + escapeHtml([s.direccion, s.localidad, s.provincia].filter(Boolean).join(", ") || "-") + '</span></div>'
                        + infoRow("Teléfono", s.telefono, "telephone")
                        + infoRow("F. Nacimiento", formatDate(s.fechaNacimiento), "calendar-heart")
                        + infoRow("F. Alta", formatDate(s.fechaAlta), "calendar-check")
                        + infoRow("Referido Por", s.referidoPorCodigo, "people")
                        + infoRow("Aprovechable", formatCurrency(d.aprovechable), "wallet2")
                        + infoRow("Consumo Mes", formatCurrency(d.consumicionDelMes), "graph-down")
                        + infoRow("Próxima Cuota", s.esExento ? "Exento de cuota" : formatDate(d.cuotaFechaProxima), "calendar-event")
                        + infoRow("Debe Cuota", s.esExento ? "Exento" : (d.debeCuota ? "Sí" : "No"), "exclamation-circle")
                        + '</div>';

                    body += '<hr><h6 class="text-muted mb-2"><i class="bi bi-chat-left-text me-1"></i>Observaciones</h6>'
                         + '<div class="mb-3"><span class="fw-semibold">' + escapeHtml(s.comentario || "-") + '</span></div>';

                    body += '<hr><h6 class="text-muted mb-2"><i class="bi bi-image me-1"></i>Documentación</h6>'
                        + '<div class="row g-2">'
                        + '<div class="col-6 text-center"><small class="text-muted d-block mb-1"><i class="bi bi-credit-card-2-front me-1"></i>DNI Anverso</small>'
                        + '<img src="' + escapeHtml(s.fotoAnversoDniUrl || '/images/dni_anverso.jpg') + '" class="img-thumbnail" style="max-height:140px" onerror="this.onerror=null;this.src=\'/images/dni_anverso.jpg\'"></div>'
                        + '<div class="col-6 text-center"><small class="text-muted d-block mb-1"><i class="bi bi-credit-card-2-back me-1"></i>DNI Reverso</small>'
                        + '<img src="' + escapeHtml(s.fotoReversoDniUrl || '/images/dni_reverso.jpg') + '" class="img-thumbnail" style="max-height:140px" onerror="this.onerror=null;this.src=\'/images/dni_reverso.jpg\'"></div>'
                        + '</div>';

                    var footer = '<button class="btn btn-outline-secondary btn-sm" data-bs-dismiss="modal"><i class="bi bi-x-lg me-1"></i>Cerrar</button>';
                    if (MexClubApi.isAdmin()) {
                        footer = '<button class="btn btn-primary btn-sm me-auto" data-action="socio-edit" data-id="' + s.id + '">'
                            + '<i class="bi bi-pencil-square me-1"></i>Editar</button>' + footer;
                    }
                    showModal("Detalle Socio", body, footer);
                })
                .catch(function (err) { showToast("Error", err.message, "danger"); });
        },

        showCreate: function () {
            var body = socioFormHtml();

            var footer = '<button class="btn btn-secondary btn-sm" data-bs-dismiss="modal"><i class="bi bi-x-lg me-1"></i>Cancelar</button>'
                + '<button class="btn btn-primary btn-sm" data-action="socio-do-create"><i class="bi bi-check-lg me-1"></i>Crear Socio</button>';

            showModal("Nuevo Socio", body, footer);

            $("#socConsMax").val(60);
            bindSocioFormFiles();
        },

        doCreate: function () {
            if (!validateSocioForm(false)) return;
            var data = collectSocioData();

            MexClubApi.createSocio(data)
                .then(function (res) {
                    if (!res.success) return;
                    return uploadSocioFiles(res.data.id).then(function () {
                        closeModal();
                        showToast("Socio creado", "Socio registrado correctamente", "success");
                        Socios.load();
                    });
                })
                .catch(function (err) { showToast("Error", err.message, "danger"); });
        },

        showEdit: function (id) {
            MexClubApi.getSocio(id)
                .then(function (res) {
                    if (!res.success) return;
                    var s = res.data;
                    _editSocioId = s.id;
                    var body = socioFormHtml(s);

                    var footer = '<button class="btn btn-secondary btn-sm" data-bs-dismiss="modal"><i class="bi bi-x-lg me-1"></i>Cancelar</button>'
                        + '<button class="btn btn-primary btn-sm" data-action="socio-do-edit"><i class="bi bi-check-lg me-1"></i>Guardar Cambios</button>';

                    showModal("Editar Socio", body, footer);
                    fillSocioForm(s);
                    bindSocioFormFiles();
                })
                .catch(function (err) { showToast("Error", err.message, "danger"); });
        },

        doEdit: function () {
            if (!validateSocioForm(true)) return;
            if (!_editSocioId) return;
            var data = collectSocioData();

            MexClubApi.updateSocio(_editSocioId, data)
                .then(function (res) {
                    if (!res.success) return;
                    return uploadSocioFiles(_editSocioId).then(function () {
                        closeModal();
                        showToast("Socio actualizado", "Datos guardados correctamente", "success");
                        Socios.load();
                    });
                })
                .catch(function (err) { showToast("Error", err.message, "danger"); });
        }
    };

    // ========== FICHAJE ==========
    var Fichaje = {
        load: function () {
            MexClubApi.getAccesos(1, 20)
                .then(function (res) {
                    if (!res.success) return;
                    var items = res.data.items || [];
                    $("#listAccesosHoy").html(
                        items.length ? renderAccesosList(items) : emptyState("Sin accesos hoy")
                    );
                })
                .catch(noop);
        },

        fichar: function () {
            var codigo = getFormValTrimmed("fichajeCodigo");
            if (!codigo) return;

            MexClubApi.buscarSocio(codigo)
                .then(function (res) {
                    if (!res.success || !res.data) {
                        showFichajeResult("Socio no encontrado", "danger");
                        $("#fichajeSocioInfo").addClass("d-none");
                        return;
                    }
                    var s = res.data;
                    var d = s.detalle || {};
                    var avatar = renderSocioAvatar(s.nombreCompleto, s.fotoUrl, true);
                    $("#fichajeSocioInfo").removeClass("d-none").html(
                        '<div class="d-flex align-items-center gap-3">'
                        + avatar
                        + '<div class="flex-grow-1">'
                        + '<div class="fw-bold">' + escapeHtml(s.nombreCompleto) + '</div>'
                        + '<small class="text-muted"><i class="bi bi-hash"></i>' + s.numSocio
                        + ' &bull; <i class="bi bi-credit-card me-1"></i>' + escapeHtml(s.documento) + '</small>'
                        + '<div class="mt-1"><span class="badge bg-success"><i class="bi bi-wallet2 me-1"></i>' + formatCurrency(d.aprovechable) + '</span></div>'
                        + '</div></div>'
                    );
                    return MexClubApi.fichar(s.id).then(function (fr) {
                        if (fr.success && fr.data) {
                            var tipo = fr.data.tipoAcceso;
                            var ico = tipo === "Entrada" ? '<i class="bi bi-box-arrow-in-right me-1"></i>' : '<i class="bi bi-box-arrow-left me-1"></i>';
                            var msg = ico + escapeHtml(s.nombreCompleto) + " — <strong>" + tipo + "</strong> registrada";
                            showFichajeResult(msg, tipo === "Entrada" ? "success" : "info");
                            $("#fichajeCodigo").val("").focus();
                            Fichaje.load();
                        }
                    });
                })
                .catch(function (err) {
                    showFichajeResult(err.message || "Error al fichar", "danger");
                });
        }
    };

    function showFichajeResult(msg, type) {
        clearTimeout(_fichajeTimer);
        var $el = $("#fichajeResult");
        $el.removeClass("d-none alert-success alert-danger alert-info alert-warning")
            .addClass("alert-" + type).html(msg);
        _fichajeTimer = setTimeout(function () { $el.addClass("d-none"); }, 4000);
    }

    // ========== ARTÍCULOS ==========
    var _editArticuloId = null;

    var Articulos = {
        _allItems: [],

        load: function () {
            Articulos._loadFamiliaFilter();
            var soloActivos = $("#articulosToggleActivos").is(":checked");
            var familiaId = $("#filterFamiliaArticulos").val() || null;
            MexClubApi.getArticulos(soloActivos, familiaId)
                .then(function (res) {
                    if (!res.success) return;
                    Articulos._allItems = res.data || [];
                    Articulos._render();
                })
                .catch(noop);
        },

        _render: function () {
            var search = ($("#searchArticulos").val() || "").trim().toLowerCase();
            var items = Articulos._allItems;
            if (search) {
                items = items.filter(function (a) {
                    return (a.nombre || "").toLowerCase().startsWith(search);
                });
            }
            if (!items.length) {
                $("#listArticulosPage").html(emptyState("Sin artículos", "box-seam"));
                return;
            }
            var html = items.map(function (a) {
                var inactive = !a.isActive;
                var cls = inactive ? ' opacity-50' : '';
                var badge = '<span class="badge bg-primary">' + formatCurrency(a.precio) + '</span> ';
                if (inactive) badge += '<span class="badge bg-secondary">Inactivo</span>';
                return '<a class="list-group-item list-group-item-action' + cls + '" data-action="articulo-detail" data-id="' + a.id + '">'
                    + '<div class="d-flex align-items-center gap-2">'
                    + letterAvatar(a.nombre)
                    + '<div class="flex-grow-1 min-width-0">'
                    + '<div class="fw-semibold text-truncate">' + escapeHtml(a.nombre) + '</div>'
                    + '<small class="text-muted">' + escapeHtml(a.familiaNombre) + '</small>'
                    + '</div>'
                    + badge
                    + '<i class="bi bi-chevron-right text-muted"></i></div></a>';
            }).join("");
            $("#listArticulosPage").html(html);
        },

        _loadFamiliaFilter: function () {
            var $sel = $("#filterFamiliaArticulos");
            if ($sel.find("option").length > 1) return;
            MexClubApi.getFamilias(true).then(function (res) {
                if (!res.success) return;
                var opts = '<option value="">Todas las familias</option>';
                (res.data || []).forEach(function (f) {
                    opts += '<option value="' + f.id + '">' + escapeHtml(f.nombre) + '</option>';
                });
                $sel.html(opts);
            });
        },

        showDetail: function (id) {
            MexClubApi.getArticulo(id).then(function (res) {
                if (!res.success) return;
                var a = res.data;
                var body = '<div class="text-center mb-3">' + letterAvatar(a.nombre, 64) + '</div>'
                    + '<h5 class="text-center">' + escapeHtml(a.nombre) + '</h5>'
                    + '<div class="text-center text-muted mb-3">'
                    + '<span class="badge bg-info"><i class="bi bi-tags me-1"></i>' + escapeHtml(a.familiaNombre) + '</span> '
                    + '<span class="badge bg-primary">' + formatCurrency(a.precio) + '</span> '
                    + (a.isActive ? '<span class="badge bg-success">Activo</span>' : '<span class="badge bg-secondary">Inactivo</span>')
                    + '</div>';
                if (a.descripcion) {
                    body += '<p class="text-muted small text-center">' + escapeHtml(a.descripcion) + '</p>';
                }
                body += '<div class="row text-center mt-2">';
                body += '<div class="col"><small class="text-muted d-block">Cant. 1</small><strong>' + a.cantidad1 + '</strong></div>';
                if (a.cantidad2) body += '<div class="col"><small class="text-muted d-block">Cant. 2</small><strong>' + a.cantidad2 + '</strong></div>';
                if (a.cantidad3) body += '<div class="col"><small class="text-muted d-block">Cant. 3</small><strong>' + a.cantidad3 + '</strong></div>';
                if (a.cantidad4) body += '<div class="col"><small class="text-muted d-block">Cant. 4</small><strong>' + a.cantidad4 + '</strong></div>';
                body += '</div>';
                var footer = '<button class="btn btn-secondary btn-sm" data-bs-dismiss="modal"><i class="bi bi-x-lg me-1"></i>Cerrar</button>'
                    + '<button class="btn btn-primary btn-sm" data-action="articulo-edit" data-id="' + a.id + '"><i class="bi bi-pencil me-1"></i>Editar</button>';
                showModal("Detalle de Artículo", body, footer);
            }).catch(function (err) { showToast("Error", err.message, "danger"); });
        },

        showEdit: function (id) {
            MexClubApi.getArticulo(id).then(function (aRes) {
                if (!aRes.success) return;
                var a = aRes.data;
                _editArticuloId = a.id;
                return MexClubApi.getFamilias(true).then(function (fRes) {
                    var familias = (fRes.data || []).map(function (f) {
                        return { value: f.id, text: (f.nombre || "").trim() };
                    });
                    var body = '<form id="formArticulo">'
                        + formSelect("Familia", "artFamilia", familias, "tags")
                        + formInput("Nombre", "artNombre", "text", true, "box-seam")
                        + formTextarea("Descripción", "artDescripcion", 3, "card-text")
                        + formInput("Precio", "artPrecio", "number", true, "currency-euro")
                        + '<div class="row"><div class="col-6">' + formInput("Cantidad 1", "artCant1", "number", true) + '</div>'
                        + '<div class="col-6">' + formInput("Cantidad 2", "artCant2", "number") + '</div></div>'
                        + '<div class="row"><div class="col-6">' + formInput("Cantidad 3", "artCant3", "number") + '</div>'
                        + '<div class="col-6">' + formInput("Cantidad 4", "artCant4", "number") + '</div></div>'
                        + '<div class="form-check form-switch mb-3">'
                        + '<input class="form-check-input" type="checkbox" id="artActivo"' + (a.isActive ? ' checked' : '') + '>'
                        + '<label class="form-check-label" for="artActivo">Activo</label>'
                        + '</div>'
                        + '</form>';
                    var footer = '<button class="btn btn-secondary btn-sm" data-bs-dismiss="modal"><i class="bi bi-x-lg me-1"></i>Cancelar</button>'
                        + '<button class="btn btn-primary btn-sm" data-action="articulo-do-edit"><i class="bi bi-check-lg me-1"></i>Guardar</button>';
                    showModal("Editar Artículo", body, footer);
                    setTimeout(function () {
                        $("#artFamilia").val(a.familiaId);
                        $("#artNombre").val((a.nombre || "").trim());
                        $("#artDescripcion").val((a.descripcion || "").trim());
                        $("#artPrecio").val(a.precio);
                        $("#artCant1").val(a.cantidad1);
                        $("#artCant2").val(a.cantidad2 || "");
                        $("#artCant3").val(a.cantidad3 || "");
                        $("#artCant4").val(a.cantidad4 || "");
                    }, 100);
                });
            }).catch(function (err) { showToast("Error", err.message, "danger"); });
        },

        doEdit: function () {
            if (!_editArticuloId) return;
            var data = {
                familiaId: getFormInt("artFamilia"),
                nombre: getFormValTrimmed("artNombre"),
                descripcion: getFormValOrNull("artDescripcion"),
                precio: getFormFloat("artPrecio"),
                cantidad1: getFormFloat("artCant1"),
                cantidad2: getFormFloat("artCant2") || null,
                cantidad3: getFormFloat("artCant3") || null,
                cantidad4: getFormFloat("artCant4") || null,
                esDecimal: false,
                isActive: $("#artActivo").is(":checked")
            };
            if (!data.nombre) { showToast("Error", "El nombre es obligatorio", "danger"); return; }
            MexClubApi.updateArticulo(_editArticuloId, data)
                .then(function (res) {
                    if (res.success) {
                        closeModal();
                        showToast("Artículo actualizado", "", "success");
                        Articulos.load();
                    }
                })
                .catch(function (err) { showToast("Error", err.message, "danger"); });
        },

        showCreate: function () {
            MexClubApi.getFamilias(true).then(function (res) {
                var familias = (res.data || []).map(function (f) {
                    return { value: f.id, text: (f.nombre || "").trim() };
                });
                var body = '<form id="formArticulo">'
                    + formSelect("Familia", "artFamilia", familias, "tags")
                    + formInput("Nombre", "artNombre", "text", true, "box-seam")
                    + formTextarea("Descripción", "artDescripcion", 3, "card-text")
                    + formInput("Precio", "artPrecio", "number", true, "currency-euro")
                    + '<div class="row"><div class="col-6">' + formInput("Cantidad 1", "artCant1", "number", true) + '</div>'
                    + '<div class="col-6">' + formInput("Cantidad 2", "artCant2", "number") + '</div></div>'
                    + '<div class="row"><div class="col-6">' + formInput("Cantidad 3", "artCant3", "number") + '</div>'
                    + '<div class="col-6">' + formInput("Cantidad 4", "artCant4", "number") + '</div></div>'
                    + '</form>';
                var footer = '<button class="btn btn-secondary btn-sm" data-bs-dismiss="modal"><i class="bi bi-x-lg me-1"></i>Cancelar</button>'
                    + '<button class="btn btn-primary btn-sm" data-action="articulo-do-create"><i class="bi bi-check-lg me-1"></i>Crear</button>';
                showModal("Nuevo Artículo", body, footer);
            });
        },

        doCreate: function () {
            var data = {
                familiaId: getFormInt("artFamilia"),
                nombre: getFormValTrimmed("artNombre"),
                descripcion: getFormValOrNull("artDescripcion"),
                precio: getFormFloat("artPrecio"),
                cantidad1: getFormFloat("artCant1"),
                cantidad2: getFormFloat("artCant2") || null,
                cantidad3: getFormFloat("artCant3") || null,
                cantidad4: getFormFloat("artCant4") || null,
                esDecimal: false
            };
            if (!data.nombre) { showToast("Error", "El nombre es obligatorio", "danger"); return; }
            MexClubApi.createArticulo(data)
                .then(function (res) {
                    if (res.success) {
                        closeModal();
                        showToast("Artículo creado", "", "success");
                        Articulos.load();
                    }
                })
                .catch(function (err) { showToast("Error", err.message, "danger"); });
        }
    };

    // ========== APORTACIONES ==========
    // Calco exacto del módulo original Club (NuevaAportacion + UltimasAportaciones)
    var _apoSocio = null;

    var Aportaciones = {
        load: function () {
            Aportaciones._limpiar();
            // Auto-cargar socio de la última aportación (calco original)
            MexClubApi.getSocioUltimaAportacion()
                .then(function (res) {
                    if (!res.success || !res.data) return;
                    return MexClubApi.getSocio(res.data);
                })
                .then(function (res) {
                    if (res && res.success && res.data) {
                        Aportaciones._mostrarSocio(res.data);
                    }
                })
                .catch(noop);
        },

        buscar: function () {
            var q = ($("#apoBuscar").val() || "").trim();
            if (!q) return;
            MexClubApi.searchSocios(q, 1)
                .then(function (res) {
                    if (!res.success || !res.data || !res.data.length) {
                        Aportaciones._limpiar();
                        Aportaciones._mostrarNoEncontrado(q);
                        return;
                    }
                    return MexClubApi.getSocio(res.data[0].id);
                })
                .then(function (res) {
                    if (res && res.success && res.data) {
                        Aportaciones._mostrarSocio(res.data);
                    }
                })
                .catch(function () {
                    Aportaciones._limpiar();
                    Aportaciones._mostrarNoEncontrado(q);
                });
        },

        _limpiar: function () {
            _apoSocio = null;
            $("#apoSocioCard").addClass("d-none");
            $("#apoNombre").text("");
            $("#apoNumSocio").text("").attr("class", "badge bg-secondary mt-1");
            $("#apoInfo").text("").attr("class", "badge mt-1");
            $("#apoAprovechable, #apoLimite, #apoConsumido").text("-");
            $("#apoEstrellas, #apoAvatar, #apoInfoRows").html("");
            $("#apoCantidad").val("");
            $("#btnAportar").prop("disabled", true);
            $("#apoNoEncontrado").addClass("d-none").html("");
            $("#apoRecientesWrap").addClass("d-none");
            $("#listAportaciones").html("");
        },

        _mostrarNoEncontrado: function (codigo) {
            $("#apoNoEncontrado").removeClass("d-none").html(
                '<div class="card"><div class="card-body text-center py-4">'
                + '<i class="bi bi-person-x" style="font-size:2.5rem;color:var(--highlight)"></i>'
                + '<h6 class="mt-2 mb-1">Socio no encontrado</h6>'
                + '<p class="text-muted mb-0 small">No se encontró ningún socio con el código o documento <strong>' + escapeHtml(codigo) + '</strong></p>'
                + '</div></div>'
            );
        },

        _mostrarSocio: function (socio) {
            _apoSocio = socio;
            var d = socio.detalle || {};

            // Reset no encontrado
            $("#apoNoEncontrado").addClass("d-none").html("");

            // Avatar
            var avatar = renderSocioAvatar(socio.nombreCompleto || socio.nombre, socio.fotoUrl, true);
            $("#apoAvatar").html(avatar);

            // Header
            $("#apoNombre").text((socio.nombre || "") + " " + (socio.primerApellido || ""));
            $("#apoNumSocio").text(socio.codigo ? escapeHtml(socio.codigo) : "Sin código");

            // Stat cards
            $("#apoAprovechable").text(d.aprovechable != null ? formatCurrency(d.aprovechable) : "-");
            $("#apoLimite").text(socio.consumicionMaximaMensual || 0);
            $("#apoConsumido").text(d.consumicionDelMes != null ? formatCurrency(d.consumicionDelMes) : "-");

            // Detail info rows
            var rows = infoRow("Teléfono", socio.telefono, "telephone")
                + infoRow("Próxima Cuota", d.cuotaFechaProxima ? formatDate(d.cuotaFechaProxima) : "-", "calendar-event");
            $("#apoInfoRows").html(rows);

            // Estrellas
            var stars = socio.estrellas || 0;
            var starsHtml = "";
            for (var i = 0; i < 5; i++) {
                starsHtml += '<i class="bi bi-star' + (i < stars ? '-fill' : '') + '"></i>';
            }
            $("#apoEstrellas").html(starsHtml);

            // Info cuota badge
            var infoCuota = Aportaciones._getInfoCuota(socio);
            var $info = $("#apoInfo");
            $info.text(infoCuota);
            if (infoCuota === "Cuotas atrasadas") {
                $info.attr("class", "badge bg-danger mt-1");
            } else if (infoCuota === "Cuotas al día") {
                $info.attr("class", "badge bg-success mt-1");
            } else if (infoCuota === "Exento de cuotas") {
                $info.attr("class", "badge bg-info mt-1");
            } else {
                $info.attr("class", "badge bg-secondary mt-1");
            }

            $("#apoSocioCard").removeClass("d-none");

            // Load recent aportaciones for this socio
            Aportaciones._cargarRecientes(socio.id);
        },

        _getInfoCuota: function (socio) {
            var d = socio.detalle;
            if (!d) return "Sin información";
            if (socio.esExento || d.exentoCuota) return "Exento de cuotas";
            if (d.cuotaFechaProxima && new Date(d.cuotaFechaProxima) >= new Date()) return "Cuotas al día";
            return "Cuotas atrasadas";
        },

        _cargarRecientes: function (socioId) {
            MexClubApi.getAportaciones(1, 20, socioId)
                .then(function (res) {
                    if (!res.success) return;
                    var items = res.data.items || [];
                    if (items.length) {
                        var html = items.map(function (a) {
                            return '<div class="list-group-item">'
                                + '<div class="d-flex align-items-center gap-2">'
                                + '<div class="flex-grow-1 min-width-0">'
                                + '<div class="fw-semibold">' + formatCurrency(a.cantidadAportada) + '</div>'
                                + '<small class="text-muted">' + formatDateTime(a.fecha) + ' &bull; Cód: ' + escapeHtml(a.codigo) + '</small>'
                                + '</div>'
                                + '<button class="btn btn-outline-danger btn-sm admin-only" data-action="aportacion-delete" data-id="' + a.id + '" title="Eliminar">'
                                + '<i class="bi bi-trash"></i></button>'
                                + '</div></div>';
                        }).join("");
                        $("#listAportaciones").html(html);
                        $("#apoRecientesWrap").removeClass("d-none");
                    } else {
                        $("#apoRecientesWrap").addClass("d-none");
                    }
                })
                .catch(noop);
        },

        aportar: function () {
            if (!_apoSocio) return;
            var cantidad = parseInt($("#apoCantidad").val());
            if (!cantidad || cantidad <= 0) {
                showToast("Error", "La cantidad debe ser mayor a cero.", "danger");
                return;
            }
            $("#btnAportar").prop("disabled", true);

            MexClubApi.createAportacion({
                socioId: _apoSocio.id,
                usuarioId: MexClubApi.getUser().id,
                cantidadAportada: cantidad
            }).then(function (res) {
                if (res.success) {
                    var codigo = res.data ? res.data.codigo : "";
                    showToast("Aportación realizada", "Cantidad: " + formatCurrency(cantidad) + (codigo ? " — Código: " + codigo : ""), "success");
                    $("#apoCantidad").val("");
                    // Reload socio to refresh Aprovechable etc
                    MexClubApi.getSocio(_apoSocio.id).then(function (sRes) {
                        if (sRes.success && sRes.data) {
                            Aportaciones._mostrarSocio(sRes.data);
                        }
                    });
                }
            }).catch(function (err) {
                showToast("Error", err.message, "danger");
                $("#btnAportar").prop("disabled", false);
            });
        },

        confirmDelete: function (id) {
            if (!confirm("¿Está seguro que desea eliminar esta aportación?")) return;
            MexClubApi.deleteAportacion(id)
                .then(function (res) {
                    if (res.success) {
                        showToast("OK", "Aportación eliminada", "success");
                        // Reload socio data + list
                        if (_apoSocio) {
                            MexClubApi.getSocio(_apoSocio.id).then(function (sRes) {
                                if (sRes.success && sRes.data) {
                                    Aportaciones._mostrarSocio(sRes.data);
                                }
                            });
                        }
                    }
                })
                .catch(function (err) { showToast("Error", err.message, "danger"); });
        }
    };

    // ========== CUOTAS ==========
    // Calco exacto del módulo original Club
    var _cuotaSocio = null;
    var _cuotaValorAnual = 50;

    var Cuotas = {
        load: function () {
            Cuotas._limpiar();
            // Auto-cargar socio de la última aportación (calco original: BuscarUltimaAportacion)
            MexClubApi.getSocioUltimaAportacion()
                .then(function (res) {
                    if (!res.success || !res.data) return;
                    return MexClubApi.getSocio(res.data);
                })
                .then(function (res) {
                    if (res && res.success && res.data) {
                        Cuotas._mostrarSocio(res.data);
                    }
                })
                .catch(noop);
        },

        buscar: function () {
            var q = ($("#cuotaBuscar").val() || "").trim();
            if (!q) return;
            MexClubApi.searchSocios(q, 1)
                .then(function (res) {
                    if (!res.success || !res.data || !res.data.length) {
                        Cuotas._limpiar();
                        Cuotas._mostrarNoEncontrado(q);
                        return;
                    }
                    return MexClubApi.getSocio(res.data[0].id);
                })
                .then(function (res) {
                    if (res && res.success && res.data) {
                        Cuotas._mostrarSocio(res.data);
                    }
                })
                .catch(function () {
                    Cuotas._limpiar();
                    Cuotas._mostrarNoEncontrado(q);
                });
        },

        _limpiar: function () {
            _cuotaSocio = null;
            _cuotaValorAnual = 50;
            $("#cuotaSocioCard").addClass("d-none");
            $("#cuotaNombre").text("");
            $("#cuotaNumSocio").text("").attr("class", "badge bg-secondary mt-1");
            $("#cuotaInfo").text("").attr("class", "badge mt-1");
            $("#cuotaAprovechable, #cuotaLimite, #cuotaConsumido").text("-");
            $("#cuotaEstrellas, #cuotaAvatar, #cuotaInfoRows").html("");
            $("#btnCuotaAnual").prop("disabled", true).find(".cuota-btn-label").text("50");
            $("#btnCuotaMensual").prop("disabled", true).find(".cuota-btn-label").text("10");
            $("#cuotaStatusMsg").addClass("d-none").html("");
            $("#cuotaBotonesWrap").removeClass("d-none");
        },

        _mostrarNoEncontrado: function (codigo) {
            $("#cuotaBotonesWrap").addClass("d-none");
            $("#cuotaStatusMsg").removeClass("d-none").html(
                '<div class="card"><div class="card-body text-center py-4">'
                + '<i class="bi bi-person-x" style="font-size:2.5rem;color:var(--highlight)"></i>'
                + '<h6 class="mt-2 mb-1">Socio no encontrado</h6>'
                + '<p class="text-muted mb-0 small">No se encontró ningún socio con el código o documento <strong>' + escapeHtml(codigo) + '</strong></p>'
                + '</div></div>'
            );
            $("#cuotaSocioCard").removeClass("d-none");
        },

        _mostrarSocio: function (socio) {
            _cuotaSocio = socio;
            var d = socio.detalle || {};

            // Avatar with fallback (uses renderSocioAvatar which has onerror)
            var avatar = renderSocioAvatar(socio.nombreCompleto || socio.nombre, socio.fotoUrl, true);
            $("#cuotaAvatar").html(avatar);

            // Header info
            $("#cuotaNombre").text((socio.nombre || "") + " " + (socio.primerApellido || ""));
            $("#cuotaNumSocio").text(socio.codigo ? escapeHtml(socio.codigo) : "Sin código");

            // Stat cards — financial data
            $("#cuotaAprovechable").text(d.aprovechable != null ? formatCurrency(d.aprovechable) : "-");
            $("#cuotaLimite").text(socio.consumicionMaximaMensual || 0);
            $("#cuotaConsumido").text(d.consumicionDelMes != null ? formatCurrency(d.consumicionDelMes) : "-");

            // Detail info rows (same data as original, presented with app's infoRow pattern)
            var rows = infoRow("Teléfono", socio.telefono, "telephone")
                + infoRow("Próxima Cuota", d.cuotaFechaProxima ? formatDate(d.cuotaFechaProxima) : "-", "calendar-event");
            $("#cuotaInfoRows").html(rows);

            // Estrellas (calco original: RatingBar)
            var stars = socio.estrellas || 0;
            var starsHtml = "";
            for (var i = 0; i < 5; i++) {
                starsHtml += '<i class="bi bi-star' + (i < stars ? '-fill' : '') + '"></i>';
            }
            $("#cuotaEstrellas").html(starsHtml);

            // Info cuota badge (calco exacto de FechaCuota.GetInfoCuota)
            var infoCuota = Cuotas._getInfoCuota(socio);
            var $info = $("#cuotaInfo");
            $info.text(infoCuota);
            if (infoCuota === "Cuotas atrasadas") {
                $info.attr("class", "badge bg-danger mt-1");
            } else if (infoCuota === "Cuotas al día") {
                $info.attr("class", "badge bg-success mt-1");
            } else if (infoCuota === "Exento de cuotas") {
                $info.attr("class", "badge bg-info mt-1");
            } else {
                $info.attr("class", "badge bg-secondary mt-1");
            }

            // Calcular valor anual y estado botones (calco exacto de MuestraSocio)
            MexClubApi.getCuotaUltimaPorSocio(socio.id).then(function (res) {
                var ultimaCuota = (res.success && res.data) ? res.data : null;
                _cuotaValorAnual = Cuotas._getValorCuotaAnual(socio, ultimaCuota);
                Cuotas._actualizarBotones(infoCuota);
            }).catch(function () {
                _cuotaValorAnual = 50;
                Cuotas._actualizarBotones(infoCuota);
            });

            $("#cuotaSocioCard").removeClass("d-none");
        },

        // Calco exacto de FechaCuota.GetInfoCuota
        _getInfoCuota: function (socio) {
            var d = socio.detalle;
            if (!d) return "Sin información";
            if (socio.esExento || d.exentoCuota) return "Exento de cuotas";
            if (d.cuotaFechaProxima && new Date(d.cuotaFechaProxima) >= new Date()) return "Cuotas al día";
            return "Cuotas atrasadas";
        },

        // Calco exacto de FechaCuota.GetValorCuotaAnual
        _getValorCuotaAnual: function (socio, ultimaCuota) {
            var d = socio.detalle;
            if (d && ultimaCuota) {
                var periodo = ultimaCuota.periodo;
                if (d.cuotaFechaProxima) {
                    var proxima = new Date(d.cuotaFechaProxima);
                    var ahora = new Date();
                    var diffDays = Math.floor((proxima - ahora) / (1000 * 60 * 60 * 24));
                    if (periodo === "Mensual" && diffDays >= 0) return 40;
                    if (periodo === "Anual" && diffDays >= 0) return 0;
                }
            }
            return 50;
        },

        // Lógica de habilitación de botones y mensaje de estado
        _actualizarBotones: function (infoCuota) {
            var $anual = $("#btnCuotaAnual");
            var $mensual = $("#btnCuotaMensual");
            var $wrap = $("#cuotaBotonesWrap");
            var $msg = $("#cuotaStatusMsg");

            if (infoCuota === "Exento de cuotas") {
                $wrap.addClass("d-none");
                $msg.removeClass("d-none").html(
                    '<div class="card"><div class="card-body text-center py-4">'
                    + '<i class="bi bi-shield-check" style="font-size:2.5rem;color:var(--info)"></i>'
                    + '<h6 class="mt-2 mb-1">Socio exento de cuotas</h6>'
                    + '<p class="text-muted mb-0 small">Este socio no necesita realizar pagos de cuota.</p>'
                    + '</div></div>'
                );
            } else if (infoCuota === "Cuotas al día" || _cuotaValorAnual === 0) {
                $wrap.addClass("d-none");
                $msg.removeClass("d-none").html(
                    '<div class="card"><div class="card-body text-center py-4">'
                    + '<i class="bi bi-check-circle-fill" style="font-size:2.5rem;color:var(--success)"></i>'
                    + '<h6 class="mt-2 mb-1">Cuotas al día</h6>'
                    + '<p class="text-muted mb-0 small">Este socio tiene sus cuotas al corriente de pago.</p>'
                    + '</div></div>'
                );
            } else {
                // Cuotas atrasadas — mostrar botones
                $msg.addClass("d-none").html("");
                $wrap.removeClass("d-none");
                $anual.prop("disabled", false).find(".cuota-btn-label").text(_cuotaValorAnual);
                $mensual.prop("disabled", false).find(".cuota-btn-label").text("10");
            }
        },

        pagarAnual: function () {
            if (!_cuotaSocio || _cuotaValorAnual === 0) return;
            Cuotas._insertarCuota(_cuotaValorAnual, 12);
        },

        pagarMensual: function () {
            if (!_cuotaSocio) return;
            Cuotas._insertarCuota(10, 1);
        },

        _insertarCuota: function (cantidad, periodo) {
            if (!_cuotaSocio) return;
            var btnId = periodo === 12 ? "#btnCuotaAnual" : "#btnCuotaMensual";
            $(btnId).prop("disabled", true);

            MexClubApi.createCuota({
                socioId: _cuotaSocio.id,
                usuarioId: MexClubApi.getUser().id,
                cantidadCuota: cantidad,
                periodo: periodo
            }).then(function (res) {
                if (res.success) {
                    showToast("OK", "Cuota insertada con éxito", "success");
                    // Recargar datos del socio para actualizar el estado
                    MexClubApi.getSocio(_cuotaSocio.id).then(function (sRes) {
                        if (sRes.success && sRes.data) {
                            Cuotas._mostrarSocio(sRes.data);
                        }
                    });
                }
            }).catch(function (err) {
                showToast("Error", err.message, "danger");
                $(btnId).prop("disabled", false);
            });
        }
    };

    // ========== RETIRADAS (POS) ==========
    var Retiradas = {
        _state: {
            socio: null,
            cart: [],
            familias: [],
            articulos: [],
            signaturePad: null,
            selectedArticuloId: null,
            cartOpen: false
        },

        load: function (page) {
            _retiradasPage = page || 1;
            MexClubApi.getRetiradas(_retiradasPage, 20)
                .then(function (res) {
                    if (!res.success) return;
                    var items = res.data.items || [];
                    var html = items.length
                        ? items.map(renderRetiradaItem).join("")
                        : emptyState("Sin retiradas");

                    if (_retiradasPage === 1) {
                        $("#listRetiradas").html(html);
                    } else {
                        $("#listRetiradas").append(html);
                    }
                    _retiradasHasMore = items.length >= 20;
                    $("#btnLoadMoreRetiradas").toggleClass("d-none", !_retiradasHasMore);
                })
                .catch(noop);
        },

        loadMore: function () {
            Retiradas.load(_retiradasPage + 1);
        },

        showCreate: function () {
            Retiradas._state = {
                socio: null,
                cart: [],
                familias: [],
                articulos: [],
                signaturePad: null,
                selectedArticuloId: null,
                cartOpen: false
            };

            Promise.all([
                MexClubApi.getFamilias(true),
                MexClubApi.getArticulos(true)
            ]).then(function (results) {
                Retiradas._state.familias = results[0].data || [];
                Retiradas._state.articulos = results[1].data || [];
                Retiradas._renderPOS();
            }).catch(function (err) {
                showToast("Error", "No se pudieron cargar los datos: " + err.message, "danger");
            });
        },

        _renderPOS: function () {
            var s = Retiradas._state;
            var familiasConArticulos = {};
            (s.articulos || []).forEach(function (a) {
                if (a && a.isActive && a.familiaId) familiasConArticulos[a.familiaId] = true;
            });
            var familiasOptions = '<option value="">Todas las familias</option>';
            familiasOptions += s.familias.map(function (f) {
                if (!f || !f.isActive || !familiasConArticulos[f.id]) return "";
                return '<option value="' + f.id + '">' + escapeHtml(f.nombre) + '</option>';
            }).join("");

            var body = '<div class="retiradas-pos">'
                + '<div class="card border-0 shadow-sm mb-3 pos-socio-search-card">'
                + '<div class="card-body p-2">'
                + '<div class="position-relative">'
                + '<div class="input-group">'
                + '<span class="input-group-text bg-white border-end-0"><i class="bi bi-search text-primary"></i></span>'
                + '<input type="text" class="form-control border-start-0 px-2" id="posSocioSearch" placeholder="Buscar socio (nombre, código, documento...)" autocomplete="off">'
                + '</div>'
                + '<div id="posSocioResults" class="list-group position-absolute w-100 shadow-lg d-none" style="z-index: 1050; top: 100%;"></div>'
                + '</div>'
                + '<div class="socio-card-pos d-none mt-3" id="posSocioCard"></div>'
                + '<div id="posMoneySummary" class="pos-money-summary d-none mt-2">'
                + '<small class="text-muted d-block">Esta retirada</small><strong id="posResumenCarrito">0.00 €</strong>'
                + '</div>'
                + '<div class="alert alert-info mt-3 mb-0" id="posSocioLockHint"><i class="bi bi-person-check me-2"></i>Primero selecciona un socio para poder operar.</div>'
                + '</div>'
                + '</div>'

                + '<div class="card border-0 shadow-sm">'
                + '<div class="card-header bg-white border-0 p-3 position-relative">'
                + '<div class="d-flex gap-2 align-items-start">'
                + '<div class="position-relative flex-grow-1">'
                + '<i class="bi bi-search position-absolute top-50 start-0 translate-middle-y ms-3 text-muted"></i>'
                + '<input type="text" class="form-control ps-5 rounded-pill bg-light border-0" id="posSearchProd" placeholder="Buscar artículo...">'
                + '<div id="posProdResults" class="list-group position-absolute w-100 shadow d-none" style="top: calc(100% + 6px); z-index: 1045;"></div>'
                + '</div>'
                + '<button type="button" class="btn btn-outline-primary position-relative" id="posCartToggle">'
                + '<i class="bi bi-cart3 fs-5"></i>'
                + '<span class="position-absolute top-0 start-100 translate-middle badge rounded-pill bg-danger" id="posCartBadge">0</span>'
                + '</button>'
                + '</div>'
                + '<div class="mt-3">'
                + '<label class="form-label small text-muted mb-1" for="posFilterFamilia">Familia</label>'
                + '<select id="posFilterFamilia" class="form-select form-select-sm">' + familiasOptions + '</select>'
                + '</div>'
                + '<div class="d-flex flex-wrap gap-2 mt-3">'
                + '<button type="button" class="btn btn-outline-success pos-quick-amount" data-amount="10">10</button>'
                + '<button type="button" class="btn btn-outline-success pos-quick-amount" data-amount="20">20</button>'
                + '<button type="button" class="btn btn-outline-success pos-quick-amount" data-amount="30">30</button>'
                + '<button type="button" class="btn btn-outline-success pos-quick-amount" data-amount="50">50</button>'
                + '<div class="small text-muted align-self-center ms-1">Importe rápido en € (se convierte según precio del artículo)</div>'
                + '</div>'
                + '<div id="posSelectedArticle" class="small mt-2 text-muted">Selecciona un artículo para usar los importes rápidos.</div>'
                + '<div id="posCartPanel" class="pos-cart-panel d-none">'
                + '<div class="d-flex justify-content-between align-items-center mb-2">'
                + '<h6 class="mb-0"><i class="bi bi-cart3 me-1"></i>Carrito</h6>'
                + '<button class="btn btn-sm btn-light border" id="posCartClose" type="button"><i class="bi bi-x-lg"></i></button>'
                + '</div>'
                + '<div class="cart-list" id="posCartList"></div>'
                + '<div class="border-top pt-2 mt-2">'
                + '<div class="d-flex justify-content-between text-muted"><span>Acumulado</span><strong id="posCount">0.00</strong></div>'
                + '<div class="d-flex justify-content-between align-items-center fs-5"><span>Total</span><strong id="posTotal" class="text-primary">0.00 €</strong></div>'
                + '</div>'
                + '</div>'
                + '</div>'
                + '<div class="card-body p-2 bg-light rounded-bottom">'
                + '<div class="product-grid" id="posProductGrid"></div>'
                + '</div>'
                + '</div>'

                + '</div>';

            var footer = '<button class="btn btn-outline-secondary" data-bs-dismiss="modal">Cancelar</button>'
                + '<button class="btn btn-primary px-5 py-2 fw-bold" id="btnPosConfirm" disabled><i class="bi bi-check-lg me-2"></i>CONFIRMAR</button>';

            showModal("Nueva Retirada", body, footer);
            $(".modal-dialog").addClass("modal-xl");

            Retiradas._renderProducts();
            Retiradas._renderCart();
            Retiradas._bindEvents();
            Retiradas._validate();

            var modalEl = document.getElementById("genericModal");
            modalEl.addEventListener("shown.bs.modal", function () {
                $("#posSocioSearch").focus();
            }, { once: true });

            modalEl.addEventListener("hidden.bs.modal", function () {
                $(document).off(".retiradas-pos");
                $(document).off(".retirada-sign");
            }, { once: true });
        },

        _bindEvents: function () {
            var $socioInput = $("#posSocioSearch");
            var $socioResults = $("#posSocioResults");
            var $prodInput = $("#posSearchProd");

            var doSocioSearch = debounce(function () {
                var q = ($socioInput.val() || "").trim();
                if (q.length < 2) {
                    $socioResults.addClass("d-none").html("");
                    return;
                }

                MexClubApi.searchSocios(q, 15).then(function (res) {
                    var data = (res.success && res.data) ? res.data : [];
                    if (!data.length) {
                        $socioResults.removeClass("d-none").html('<div class="list-group-item text-muted text-center py-3">No se encontraron socios</div>');
                        return;
                    }

                    var term = q.toLowerCase();
                    var list = data.sort(function (a, b) {
                        var aName = (a.nombreCompleto || "").toLowerCase();
                        var bName = (b.nombreCompleto || "").toLowerCase();
                        var aStarts = aName.indexOf(term) === 0;
                        var bStarts = bName.indexOf(term) === 0;
                        if (aStarts !== bStarts) return aStarts ? -1 : 1;
                        return aName.localeCompare(bName);
                    });

                    var html = list.map(function (socio) {
                        var img = socio.fotoUrl || "/images/sin_foto.jpg";
                        return '<button type="button" class="list-group-item list-group-item-action d-flex align-items-center gap-2 p-1" onclick="MexClub.Retiradas._selectSocioId(' + socio.id + ')">'
                            + '<img src="' + img + '" class="rounded-circle object-fit-cover" style="width:32px;height:32px" onerror="this.src=\'/images/sin_foto.jpg\'">'
                            + '<div><div class="fw-semibold text-dark small">' + escapeHtml(socio.nombreCompleto) + '</div>'
                            + '<small class="text-muted">' + escapeHtml(socio.codigo) + ' • ' + escapeHtml(socio.documento) + '</small></div>'
                            + '</button>';
                    }).join("");
                    $socioResults.removeClass("d-none").html(html);
                }).catch(noop);
            }, 220);

            $socioInput.on("input", doSocioSearch);
            $socioInput.on("focus", function () {
                if (($socioInput.val() || "").trim().length >= 2) $socioResults.removeClass("d-none");
            });

            $prodInput.on("input", debounce(function () {
                Retiradas._renderProducts();
                Retiradas._renderProductSearchResults();
            }, 180));

            $prodInput.on("focus", function () {
                if (($prodInput.val() || "").trim().length > 0) Retiradas._renderProductSearchResults();
            });

            $("#posFilterFamilia").on("change", function () {
                Retiradas._renderProducts();
            });

            $(".pos-quick-amount").on("click", function () {
                var amount = parseFloat($(this).data("amount")) || 0;
                Retiradas._addQuickAmount(amount);
            });

            $("#posCartToggle").on("click", function () {
                if (!Retiradas._requireSocio("abrir el carrito")) return;
                Retiradas._toggleCartPanel();
            });

            $("#posCartClose").on("click", function () {
                Retiradas._state.cartOpen = false;
                $("#posCartPanel").addClass("d-none");
            });

            $("#btnPosConfirm").on("click", Retiradas._handleConfirmClick);

            $(document).off("click.retiradas-pos").on("click.retiradas-pos", function (e) {
                if (!$(e.target).closest("#posSocioResults, #posSocioSearch").length) {
                    $("#posSocioResults").addClass("d-none");
                }
                if (!$(e.target).closest("#posProdResults, #posSearchProd").length) {
                    $("#posProdResults").addClass("d-none");
                }
                if (!$(e.target).closest("#posCartPanel, #posCartToggle").length && Retiradas._state.cartOpen) {
                    Retiradas._state.cartOpen = false;
                    $("#posCartPanel").addClass("d-none");
                }
            });
        },

        _requireSocio: function (actionLabel) {
            if (Retiradas._state.socio) return true;
            if (actionLabel) {
                showToast("Socio requerido", "Debes seleccionar un socio para " + actionLabel + ".", "warning");
            }
            return false;
        },

        _selectSocioId: function (id) {
            MexClubApi.getSocio(id).then(function (res) {
                if (res.success && res.data) Retiradas._selectSocio(res.data);
            });
            $("#posSocioResults").addClass("d-none");
        },

        _selectSocio: function (socio) {
            Retiradas._state.socio = socio;
            var d = socio.detalle || {};
            var img = socio.fotoUrl || "/images/sin_foto.jpg";
            var stars = "";
            for (var i = 0; i < 5; i++) stars += '<i class="bi bi-star' + (i < socio.estrellas ? '-fill text-warning' : ' text-muted') + '"></i>';

            var html = '<div class="d-flex align-items-center gap-3 p-2">'
                + '<img src="' + img + '" class="rounded-circle shadow-sm object-fit-cover" style="width:80px;height:80px;border:3px solid #fff;" onerror="this.src=\'/images/sin_foto.jpg\'">'
                + '<div class="flex-grow-1">'
                + '<div class="d-flex justify-content-between">'
                + '<h4 class="mb-0 fw-bold text-dark">' + escapeHtml(socio.nombreCompleto) + '</h4>'
                + '<button class="btn btn-outline-danger btn-sm" onclick="MexClub.Retiradas._clearSocio()" title="Cambiar socio"><i class="bi bi-x-lg"></i></button>'
                + '</div>'
                + '<div class="text-muted mb-2">' + escapeHtml(socio.codigo) + ' • ' + escapeHtml(socio.documento) + ' <span class="ms-2">' + stars + '</span></div>'
                + '<div class="d-flex gap-2 flex-wrap">'
                + '<div class="badge bg-success bg-opacity-10 text-success border border-success p-2 px-3 rounded-pill"><small class="d-block text-uppercase fw-bold" style="font-size:0.65rem">Disponible</small><span class="fs-6 fw-bold" id="posSocioDisponible">' + formatCurrency(d.aprovechable) + '</span></div>'
                + '<div class="badge bg-danger bg-opacity-10 text-danger border border-danger p-2 px-3 rounded-pill"><small class="d-block text-uppercase fw-bold" style="font-size:0.65rem">Consumido</small><span class="fs-6 fw-bold" id="posSocioConsumido">' + Retiradas._formatAcumulado(d.consumicionDelMes || 0) + ' g</span></div>'
                + '<div class="badge bg-secondary bg-opacity-10 text-secondary border border-secondary p-2 px-3 rounded-pill"><small class="d-block text-uppercase fw-bold" style="font-size:0.65rem">Límite</small><span class="fs-6 fw-bold">' + Retiradas._formatAcumulado(socio.consumicionMaximaMensual || 0) + ' g</span></div>'
                + '</div>'
                + '</div>'
                + '</div>';

            $("#posSocioCard").removeClass("d-none").html(html);
            $("#posSocioSearch").closest(".position-relative").addClass("d-none");
            Retiradas._renderProducts();
            Retiradas._renderCart();
            Retiradas._validate();
        },

        _clearSocio: function () {
            Retiradas._state.socio = null;
            Retiradas._state.cart = [];
            Retiradas._state.selectedArticuloId = null;
            Retiradas._state.cartOpen = false;
            $("#posSocioCard").addClass("d-none").html("");
            $("#posSocioSearch").closest(".position-relative").removeClass("d-none");
            $("#posSocioSearch").val("").focus();
            $("#posCartPanel").addClass("d-none");
            $("#posSelectedArticle").text("Selecciona un artículo para usar los importes rápidos.");
            Retiradas._renderProducts();
            Retiradas._renderCart();
            Retiradas._validate();
        },

        _renderProductSearchResults: function () {
            if (!Retiradas._state.socio) {
                $("#posProdResults").addClass("d-none").html("");
                return;
            }
            var q = ($("#posSearchProd").val() || "").trim().toLowerCase();
            var $results = $("#posProdResults");
            if (!q) {
                $results.addClass("d-none").html("");
                return;
            }

            var list = Retiradas._state.articulos
                .filter(function (a) {
                    return a.isActive && (a.nombre || "").toLowerCase().indexOf(q) !== -1;
                })
                .sort(function (a, b) {
                    var aName = (a.nombre || "").toLowerCase();
                    var bName = (b.nombre || "").toLowerCase();
                    var aStarts = aName.indexOf(q) === 0;
                    var bStarts = bName.indexOf(q) === 0;
                    if (aStarts !== bStarts) return aStarts ? -1 : 1;
                    return aName.localeCompare(bName);
                })
                .slice(0, 10);

            if (!list.length) {
                $results.removeClass("d-none").html('<div class="list-group-item text-muted text-center py-2">Sin resultados</div>');
                return;
            }

            var html = list.map(function (a) {
                return '<button type="button" class="list-group-item list-group-item-action d-flex justify-content-between align-items-center" onclick="MexClub.Retiradas._selectArticuloFromSearch(' + a.id + ')">'
                    + '<span class="text-truncate me-2">' + escapeHtml(a.nombre) + '</span>'
                    + '<small class="text-muted">' + formatCurrency(a.precio) + '</small>'
                    + '</button>';
            }).join("");

            $results.removeClass("d-none").html(html);
        },

        _selectArticuloFromSearch: function (articuloId) {
            if (!Retiradas._requireSocio("seleccionar artículos")) return;
            var art = Retiradas._state.articulos.find(function (a) { return a.id === articuloId; });
            if (!art) return;

            Retiradas._state.selectedArticuloId = art.id;
            $("#posSearchProd").val(art.nombre);
            $("#posProdResults").addClass("d-none").html("");

            $("#posFilterFamilia").val(String(art.familiaId || ""));

            $("#posSelectedArticle").html('<span class="fw-semibold">Seleccionado:</span> ' + escapeHtml(art.nombre) + ' <span class="text-muted">(' + formatCurrency(art.precio) + '/g)</span>');
            Retiradas._renderProducts();
        },

        _renderProducts: function () {
            var socioSelected = !!Retiradas._state.socio;
            var famId = parseInt($("#posFilterFamilia").val(), 10) || 0;
            var q = ($("#posSearchProd").val() || "").trim().toLowerCase();

            var list = Retiradas._state.articulos.filter(function (a) {
                if (!a.isActive) return false;
                if (famId && a.familiaId !== famId) return false;
                if (q && (a.nombre || "").toLowerCase().indexOf(q) === -1) return false;
                return true;
            });

            if (!list.length) {
                $("#posProductGrid").html('<div class="col-12 text-center text-muted py-5"><i class="bi bi-box-seam display-4 d-block mb-3 opacity-25"></i>No hay productos</div>');
                return;
            }

            var html = list.map(function (a) {
                var hues = [200, 150, 40, 280, 10, 340, 100, 25];
                var hue = hues[(a.familiaId || 0) % hues.length];
                var style = 'border-top: 4px solid hsl(' + hue + ', 70%, 50%);';
                var selectedCls = Retiradas._state.selectedArticuloId === a.id ? " pos-product-selected" : "";
                var lockCls = socioSelected ? "" : " pos-product-locked";

                return '<div class="product-card h-100 shadow-sm border-0' + selectedCls + lockCls + '" data-art-id="' + a.id + '" style="' + style + '" onclick="MexClub.Retiradas._addToCart(' + a.id + ')">'
                    + '<div class="card-body p-2 d-flex flex-column h-100">'
                    + '<small class="text-muted text-uppercase fw-bold" style="font-size:0.65rem">' + escapeHtml(a.familiaNombre || "General") + '</small>'
                    + '<div class="prod-name flex-grow-1 fw-bold text-dark my-1" title="' + escapeHtml(a.nombre) + '">' + escapeHtml(a.nombre) + '</div>'
                    + '<div class="d-flex justify-content-between align-items-end mt-2">'
                    + '<span class="badge bg-light text-dark border">' + formatCurrency(a.precio) + '/g</span>'
                    + '<i class="bi bi-plus-circle-fill text-primary fs-5"></i>'
                    + '</div>'
                    + '</div>'
                    + '</div>';
            }).join("");

            $("#posProductGrid").html(html);
        },

        _addToCart: function (articuloId) {
            if (!Retiradas._requireSocio("añadir artículos")) return;
            var art = Retiradas._state.articulos.find(function (a) { return a.id === articuloId; });
            if (!art) return;

            Retiradas._state.selectedArticuloId = art.id;
            $("#posSelectedArticle").html('<span class="fw-semibold">Seleccionado:</span> ' + escapeHtml(art.nombre) + ' <span class="text-muted">(' + formatCurrency(art.precio) + '/g)</span>');
            Retiradas._renderProducts();
        },

        _addQuickAmount: function (importe) {
            if (!Retiradas._requireSocio("usar importes rápidos")) return;
            if (!(importe > 0)) return;

            var art = Retiradas._state.articulos.find(function (a) { return a.id === Retiradas._state.selectedArticuloId; });
            if (!art) {
                showToast("Selecciona artículo", "Debes seleccionar un artículo antes de usar 10/20/30/50.", "warning");
                return;
            }

            if (!(art.precio > 0)) {
                showToast("Precio inválido", "El artículo seleccionado no tiene precio válido.", "warning");
                return;
            }

            var pago = Retiradas._round2(importe);
            var qty = Retiradas._floor4(pago / art.precio);
            if (!(qty > 0)) return;

            var existing = Retiradas._state.cart.find(function (item) { return item.id === art.id; });
            if (existing) {
                existing.pagado = Retiradas._round2((existing.pagado || existing.total || 0) + pago);
                existing.cantidad = Retiradas._floor4(existing.pagado / existing.precio);
                existing.total = existing.pagado;
            } else {
                Retiradas._state.cart.push({
                    id: art.id,
                    nombre: art.nombre,
                    precio: art.precio,
                    cantidad: qty,
                    pagado: pago,
                    total: pago
                });
            }

            Retiradas._renderCart();
        },

        _removeFromCart: function (idx) {
            if (!Retiradas._requireSocio("modificar el carrito")) return;
            Retiradas._state.cart.splice(idx, 1);
            Retiradas._renderCart();
        },

        _toggleCartPanel: function () {
            Retiradas._state.cartOpen = !Retiradas._state.cartOpen;
            $("#posCartPanel").toggleClass("d-none", !Retiradas._state.cartOpen);
        },

        _formatQty: function (val) {
            var num = parseFloat(val || 0);
            if (Math.abs(num % 1) < 0.00001) return String(parseInt(num, 10));
            return num.toFixed(2);
        },

        _formatAcumulado: function (val) {
            return Retiradas._formatQty(val);
        },

        _round2: function (val) {
            return Math.round(parseFloat(val || 0) * 100) / 100;
        },

        _round4: function (val) {
            return Math.round(parseFloat(val || 0) * 10000) / 10000;
        },

        _floor4: function (val) {
            return Math.floor(parseFloat(val || 0) * 10000) / 10000;
        },

        _getCartTotal: function () {
            return Retiradas._round2((Retiradas._state.cart || []).reduce(function (sum, item) {
                return sum + (item.total || 0);
            }, 0));
        },

        _getSocioFinancials: function (cartTotal) {
            var socio = Retiradas._state.socio;
            var d = socio && socio.detalle ? socio.detalle : {};
            var limiteMensualGramos = Retiradas._round4(socio ? (socio.consumicionMaximaMensual || 0) : 0);
            var saldoDisponible = Retiradas._round2(d.aprovechable || 0);
            var consumidoActualGramos = Retiradas._round4(d.consumicionDelMes || 0);
            var total = Retiradas._round2(cartTotal || 0);
            var gramosCarrito = Retiradas._round4((Retiradas._state.cart || []).reduce(function (sum, item) {
                return sum + (item.cantidad || 0);
            }, 0));
            var saldoRestante = Retiradas._round2(saldoDisponible - total);
            var consumidoTrasRetiradaGramos = Retiradas._round4(consumidoActualGramos + gramosCarrito);
            var superaLimite = limiteMensualGramos > 0 && (consumidoTrasRetiradaGramos - limiteMensualGramos) > 0.00001;
            var saldoInsuficiente = saldoRestante < -0.01;

            return {
                limiteMensual: limiteMensualGramos,
                saldoDisponible: saldoDisponible,
                consumidoActual: consumidoActualGramos,
                saldoRestante: saldoRestante,
                consumidoTrasRetirada: consumidoTrasRetiradaGramos,
                superaLimite: superaLimite,
                saldoInsuficiente: saldoInsuficiente
            };
        },

        _renderCart: function () {
            var cart = Retiradas._state.cart;
            var $list = $("#posCartList");
            var $total = $("#posTotal");
            var $count = $("#posCount");
            var $badge = $("#posCartBadge");

            if (!cart.length) {
                $list.html('<div class="text-center text-muted py-4"><i class="bi bi-basket fs-2 d-block mb-2"></i>Carrito vacío</div>');
                $total.text("0.00 €");
                $count.text("0.00");
                $badge.text("0");
                Retiradas._validate();
                return;
            }

            var totalSum = 0;
            var lines = cart.length;
            var gramsSum = 0;

            var html = cart.map(function (item, idx) {
                totalSum += item.total;
                gramsSum += item.cantidad;
                return '<div class="cart-item p-2 border-bottom">'
                    + '<div class="d-flex justify-content-between align-items-start gap-2">'
                    + '<div class="min-width-0">'
                    + '<div class="fw-semibold text-truncate" title="' + escapeHtml(item.nombre) + '">' + escapeHtml(item.nombre) + '</div>'
                    + '<small class="text-muted d-block">Precio: ' + formatCurrency(item.precio) + '/g</small>'
                    + '<small class="text-muted">Acumulado: ' + Retiradas._formatAcumulado(item.cantidad) + '</small>'
                    + '</div>'
                    + '<div class="text-end">'
                    + '<div class="fw-bold text-primary">' + formatCurrency(item.total) + '</div>'
                    + '<button class="btn btn-sm btn-link text-danger p-0" onclick="MexClub.Retiradas._removeFromCart(' + idx + ')"><i class="bi bi-trash"></i> Eliminar</button>'
                    + '</div>'
                    + '</div>'
                    + '</div>';
            }).join("");

            $list.html(html);
            totalSum = Retiradas._round2(totalSum);
            $total.text(formatCurrency(totalSum));
            $count.text(Retiradas._formatAcumulado(gramsSum));
            $badge.text(String(lines));
            Retiradas._validate();
        },

        _updateMoneySummary: function (cartTotal) {
            var socio = Retiradas._state.socio;
            var carrito = Retiradas._round2(cartTotal || 0);
            var gramosCarrito = (Retiradas._state.cart || []).reduce(function (sum, item) { return sum + (item.cantidad || 0); }, 0);
            var fin = Retiradas._getSocioFinancials(carrito);

            $("#posResumenCarrito").text(formatCurrency(carrito) + " · " + Retiradas._formatAcumulado(gramosCarrito));
            $("#posSocioDisponible").text(formatCurrency(fin.saldoRestante));
            $("#posSocioConsumido").text(Retiradas._formatAcumulado(fin.consumidoTrasRetirada) + " g");

            $("#posMoneySummary").toggleClass("d-none", !socio);
        },

        _validate: function () {
            var socio = Retiradas._state.socio;
            var cart = Retiradas._state.cart;
            var btn = $("#btnPosConfirm");
            var locked = !socio;
            var total = Retiradas._getCartTotal();

            Retiradas._updateMoneySummary(total);

            $("#posSocioLockHint").toggleClass("d-none", !locked);
            $("#posSearchProd").prop("disabled", locked);
            $(".pos-quick-amount").prop("disabled", locked);
            $("#posCartToggle").prop("disabled", locked);

            if (locked) {
                Retiradas._state.cartOpen = false;
                $("#posCartPanel").addClass("d-none");
            }

            if (!socio || !cart.length) {
                var label = socio ? 'CONFIRMAR' : 'SELECCIONA UN SOCIO';
                btn.prop("disabled", true).removeClass("btn-danger btn-warning").addClass("btn-primary").html('<i class="bi bi-check-lg me-2"></i>' + label);
                return;
            }

            var fin = Retiradas._getSocioFinancials(total);

            if (fin.saldoInsuficiente) {
                btn.prop("disabled", true).removeClass("btn-primary btn-warning").addClass("btn-danger")
                    .html('<i class="bi bi-x-circle me-2"></i>SALDO INSUFICIENTE');
                return;
            }

            if (fin.superaLimite) {
                btn.prop("disabled", false).removeClass("btn-primary btn-danger").addClass("btn-warning")
                    .html('<i class="bi bi-exclamation-triangle me-2"></i>CONFIRMAR · SUPERA LÍMITE');
                return;
            }

            btn.prop("disabled", false).removeClass("btn-danger btn-warning").addClass("btn-primary").html('<i class="bi bi-check-lg me-2"></i>CONFIRMAR');
        },

        _initSignature: function (canvasId) {
            var canvas = document.getElementById(canvasId || "posSigPad");
            if (!canvas) return;

            var ratio = Math.max(window.devicePixelRatio || 1, 1);
            canvas.width = canvas.offsetWidth * ratio;
            canvas.height = canvas.offsetHeight * ratio;
            canvas.getContext("2d").scale(ratio, ratio);

            var ctx = canvas.getContext("2d");
            ctx.lineWidth = 2;
            ctx.lineCap = "round";
            ctx.strokeStyle = "#000";

            var isDrawing = false;
            var lastX = 0;
            var lastY = 0;

            function getCoords(e) {
                var rect = canvas.getBoundingClientRect();
                var clientX = e.touches ? e.touches[0].clientX : e.clientX;
                var clientY = e.touches ? e.touches[0].clientY : e.clientY;
                return {
                    x: clientX - rect.left,
                    y: clientY - rect.top
                };
            }

            $(canvas).off(".retirada-sign").on("mousedown.retirada-sign touchstart.retirada-sign", function (e) {
                isDrawing = true;
                var coords = getCoords(e);
                lastX = coords.x;
                lastY = coords.y;
                e.preventDefault();
            });

            $(document).off("mousemove.retirada-sign touchmove.retirada-sign mouseup.retirada-sign touchend.retirada-sign");
            $(document).on("mousemove.retirada-sign touchmove.retirada-sign", function (e) {
                if (!isDrawing) return;
                var coords = getCoords(e);
                ctx.beginPath();
                ctx.moveTo(lastX, lastY);
                ctx.lineTo(coords.x, coords.y);
                ctx.stroke();
                lastX = coords.x;
                lastY = coords.y;
            });
            $(document).on("mouseup.retirada-sign touchend.retirada-sign", function () {
                isDrawing = false;
            });

            Retiradas._state.signaturePad = {
                clear: function () {
                    ctx.clearRect(0, 0, canvas.width, canvas.height);
                },
                isEmpty: function () {
                    var pixelBuffer = new Uint32Array(ctx.getImageData(0, 0, canvas.width, canvas.height).data.buffer);
                    return !pixelBuffer.some(function (color) { return color !== 0; });
                },
                toDataURL: function () {
                    return canvas.toDataURL("image/png");
                }
            };
        },

        _showLimitWarningModal: function (onAccept) {
            var modalId = "retiradaLimitModal";
            var existing = document.getElementById(modalId);
            if (!existing) {
                var html = ''
                    + '<div class="modal fade" id="' + modalId + '" tabindex="-1" aria-hidden="true">'
                    + '<div class="modal-dialog modal-dialog-centered">'
                    + '<div class="modal-content">'
                    + '<div class="modal-header">'
                    + '<h5 class="modal-title"><i class="bi bi-exclamation-triangle text-warning me-2"></i>Límite mensual</h5>'
                    + '<button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button>'
                    + '</div>'
                    + '<div class="modal-body">Esta retirada superará el límite mensual del socio. ¿Deseas continuar?</div>'
                    + '<div class="modal-footer">'
                    + '<button type="button" class="btn btn-outline-secondary" data-bs-dismiss="modal">No</button>'
                    + '<button type="button" class="btn btn-warning" id="btnRetiradaLimitAccept">Sí, continuar</button>'
                    + '</div>'
                    + '</div>'
                    + '</div>'
                    + '</div>';
                $("body").append(html);
                existing = document.getElementById(modalId);
            }

            var modal = bootstrap.Modal.getOrCreateInstance(existing, { backdrop: "static", keyboard: false });
            $("#btnRetiradaLimitAccept").off("click").on("click", function () {
                modal.hide();
                if (onAccept) onAccept();
            });
            modal.show();
        },

        _showSignatureModal: function (onSigned) {
            var modalId = "retiradaSignatureModal";
            var existing = document.getElementById(modalId);
            if (!existing) {
                var html = ''
                    + '<div class="modal fade" id="' + modalId + '" tabindex="-1" aria-hidden="true">'
                    + '<div class="modal-dialog modal-dialog-centered">'
                    + '<div class="modal-content">'
                    + '<div class="modal-header">'
                    + '<h5 class="modal-title"><i class="bi bi-pen text-primary me-2"></i>Firma obligatoria</h5>'
                    + '<button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button>'
                    + '</div>'
                    + '<div class="modal-body">'
                    + '<div class="signature-pad-wrapper">'
                    + '<canvas class="signature-pad" id="retiradaSigPadModal"></canvas>'
                    + '</div>'
                    + '<small class="text-muted">Debes firmar para confirmar la retirada.</small>'
                    + '</div>'
                    + '<div class="modal-footer">'
                    + '<button type="button" class="btn btn-outline-secondary" data-bs-dismiss="modal">Cancelar</button>'
                    + '<button type="button" class="btn btn-link text-danger me-auto" id="btnRetiradaSigClear"><i class="bi bi-eraser me-1"></i>Borrar</button>'
                    + '<button type="button" class="btn btn-primary" id="btnRetiradaSigAccept">Firmar y confirmar</button>'
                    + '</div>'
                    + '</div>'
                    + '</div>'
                    + '</div>';
                $("body").append(html);
                existing = document.getElementById(modalId);
            }

            var modal = bootstrap.Modal.getOrCreateInstance(existing, { backdrop: "static", keyboard: false });
            $(existing).off("shown.bs.modal.retirada-signature").on("shown.bs.modal.retirada-signature", function () {
                Retiradas._initSignature("retiradaSigPadModal");
            });

            $("#btnRetiradaSigClear").off("click").on("click", function () {
                if (Retiradas._state.signaturePad) Retiradas._state.signaturePad.clear();
            });

            $("#btnRetiradaSigAccept").off("click").on("click", function () {
                if (!Retiradas._state.signaturePad || Retiradas._state.signaturePad.isEmpty()) {
                    showToast("Firma obligatoria", "Debes firmar antes de confirmar la retirada.", "warning");
                    return;
                }
                var firma = Retiradas._state.signaturePad.toDataURL();
                modal.hide();
                if (onSigned) onSigned(firma);
            });

            modal.show();
        },

        _handleConfirmClick: function () {
            var socio = Retiradas._state.socio;
            var cart = Retiradas._state.cart;
            if (!socio || !cart.length) return;

            var total = Retiradas._getCartTotal();
            var fin = Retiradas._getSocioFinancials(total);
            if (fin.saldoInsuficiente) {
                showToast("Saldo insuficiente", "No hay saldo disponible suficiente para esta retirada.", "warning");
                Retiradas._validate();
                return;
            }

            var proceed = function (allowLimitExceed) {
                Retiradas._showSignatureModal(function (firma) {
                    Retiradas._submit(firma, !!allowLimitExceed);
                });
            };

            if (fin.superaLimite) {
                Retiradas._showLimitWarningModal(function () { proceed(true); });
                return;
            }

            proceed(false);
        },

        _submit: function (firmaBase64, permitirExcesoLimiteMensual) {
            var socio = Retiradas._state.socio;
            var cart = Retiradas._state.cart;
            if (!socio || !cart.length) return;

            if (!firmaBase64) {
                showToast("Firma obligatoria", "Debes firmar antes de confirmar la retirada.", "warning");
                return;
            }

            $("#btnPosConfirm").prop("disabled", true).html('<span class="spinner-border spinner-border-sm me-1"></span>Procesando...');

            var payload = {
                socioId: socio.id,
                usuarioId: MexClubApi.getUser().id,
                firmaBase64: firmaBase64,
                permitirExcesoLimiteMensual: !!permitirExcesoLimiteMensual,
                items: cart.map(function (item) {
                    return { articuloId: item.id, cantidad: item.cantidad };
                })
            };

            MexClubApi.createRetiradaBatch(payload)
                .then(function (res) {
                    if (res.success) {
                        var created = res.data || [];
                        closeModal();
                        Retiradas.load();
                        Retiradas._showRetiradaCodesModal(created);
                    } else {
                        var msg = (res && res.message ? String(res.message) : "").toLowerCase();
                        if (msg.indexOf("límite mensual excedido") !== -1 || msg.indexOf("limite mensual excedido") !== -1) {
                            showToast("Límite mensual", "Se superó el límite mensual.", "warning");
                        } else if (msg.indexOf("saldo insuficiente") !== -1) {
                            showToast("Saldo insuficiente", "No hay saldo disponible suficiente para esta retirada.", "warning");
                        } else {
                            showToast("Error", "No se pudo completar la operación", "danger");
                        }
                        Retiradas._validate();
                    }
                })
                .catch(function (err) {
                    var msg = (err && err.message ? String(err.message) : "").toLowerCase();
                    if (msg.indexOf("límite mensual excedido") !== -1 || msg.indexOf("limite mensual excedido") !== -1) {
                        showToast("Límite mensual", "Se superó el límite mensual.", "warning");
                    } else if (msg.indexOf("saldo insuficiente") !== -1) {
                        showToast("Saldo insuficiente", "No hay saldo disponible suficiente para esta retirada.", "warning");
                    } else {
                        showToast("Error", err.message, "danger");
                    }
                    Retiradas._validate();
                });
        },

        _showRetiradaCodesModal: function (createdItems) {
            var items = createdItems || [];
            if (!items.length) {
                showToast("Retirada exitosa", "Retirada registrada correctamente.", "success");
                return;
            }

            var codes = items.map(function (r) {
                return "RET-" + r.id;
            });

            var lines = codes.map(function (code) {
                return '<li class="mb-1"><strong>' + escapeHtml(code) + '</strong></li>';
            }).join("");

            var body = '<div class="alert alert-success mb-3"><i class="bi bi-check-circle me-2"></i>Retirada registrada correctamente.</div>'
                + '<p class="mb-2">Apunta el código de retirada:</p>'
                + '<ul class="mb-0 ps-3">' + lines + '</ul>';

            var footer = '<button class="btn btn-primary" data-bs-dismiss="modal">Aceptar</button>';
            showModal("Código de retirada", body, footer);
        }
    };

    // ========== FAMILIAS (standalone page) ==========
    var _editFamiliaId = null;
    var _editFamiliaDescuento = null;

    var Familias = {
        _allItems: [],

        load: function () {
            var soloActivas = $("#familiasToggleActivas").is(":checked");
            MexClubApi.getFamilias(soloActivas ? true : false)
                .then(function (res) {
                    if (!res.success) return;
                    Familias._allItems = res.data || [];
                    Familias._render();
                })
                .catch(noop);
        },

        _render: function () {
            var search = ($("#searchFamilias").val() || "").trim().toLowerCase();
            var items = Familias._allItems;

            if (search) {
                items = items.filter(function (f) {
                    return (f.nombre || "").toLowerCase().startsWith(search);
                });
            }

            if (!items.length) {
                $("#listFamiliasPage").html(emptyState("Sin familias", "tags"));
                return;
            }

            var html = items.map(function (f) {
                var inactive = !f.isActive;
                var cls = inactive ? ' opacity-50' : '';
                var badge = '';
                if (inactive) badge += '<span class="badge bg-secondary">Inactiva</span>';
                return '<a class="list-group-item list-group-item-action' + cls + '" data-action="familia-edit" data-id="' + f.id + '" data-familia-id="' + f.id + '">'
                    + '<div class="d-flex align-items-center gap-2">'
                    + letterAvatar(f.nombre)
                    + '<div class="flex-grow-1 min-width-0"><div class="fw-semibold text-truncate">' + escapeHtml(f.nombre) + '</div></div>'
                    + badge
                    + '<i class="bi bi-chevron-right text-muted"></i></div></a>';
            }).join("");
            $("#listFamiliasPage").html(html);
        },

        showEdit: function (id) {
            MexClubApi.getFamilia(id).then(function (res) {
                if (!res.success) return;
                var f = res.data;
                _editFamiliaId = f.id;
                _editFamiliaDescuento = f.descuento || 0;
                var body = '<form id="formFamilia">'
                    + formInput("Nombre", "famNombre", "text", true, "tag")
                    + '<div class="form-check form-switch mb-3">'
                    + '<input class="form-check-input" type="checkbox" id="famActiva"' + (f.isActive ? ' checked' : '') + '>'
                    + '<label class="form-check-label" for="famActiva">Activa</label>'
                    + '</div>'
                    + '<div class="alert alert-warning d-none" id="famDeactivateWarning"><i class="bi bi-exclamation-triangle me-1"></i><span id="famDeactivateMsg"></span></div>'
                    + '</form>';
                var footer = '<button class="btn btn-secondary btn-sm" data-bs-dismiss="modal"><i class="bi bi-x-lg me-1"></i>Cancelar</button>'
                    + '<button class="btn btn-primary btn-sm" data-action="familia-do-edit"><i class="bi bi-check-lg me-1"></i>Guardar</button>';
                showModal("Editar Familia", body, footer);
                setTimeout(function () {
                    var $inp = $("#famNombre");
                    $inp.val((f.nombre || "").trim()).focus();
                    var el = $inp[0];
                    if (el.setSelectionRange) el.setSelectionRange(el.value.length, el.value.length);
                    // Show warning when unchecking active if has articles
                    $("#famActiva").on("change", function () {
                        if (!$(this).is(":checked")) {
                            MexClubApi.countFamiliaArticulos(f.id).then(function (cRes) {
                                var count = (cRes.success ? cRes.data : 0);
                                if (count > 0) {
                                    $("#famDeactivateMsg").text("Esta familia tiene " + count + " artículo" + (count > 1 ? "s" : "") + " activo" + (count > 1 ? "s" : "") + " que también serán desactivados.");
                                    $("#famDeactivateWarning").removeClass("d-none");
                                } else {
                                    $("#famDeactivateWarning").addClass("d-none");
                                }
                            });
                        } else {
                            $("#famDeactivateWarning").addClass("d-none");
                        }
                    });
                }, 100);
            }).catch(function (err) { showToast("Error", err.message, "danger"); });
        },

        doEdit: function () {
            if (!_editFamiliaId) return;
            var data = {
                nombre: getFormValTrimmed("famNombre"),
                descuento: _editFamiliaDescuento,
                isActive: $("#famActiva").is(":checked")
            };
            if (!data.nombre) { showToast("Error", "El nombre es obligatorio", "danger"); return; }
            MexClubApi.updateFamilia(_editFamiliaId, data)
                .then(function (res) {
                    if (res.success) {
                        closeModal();
                        showToast("Familia actualizada", "", "success");
                        Familias.load();
                    }
                })
                .catch(function (err) { showToast("Error", err.message, "danger"); });
        },

        showCreate: function () {
            var body = '<form id="formFamilia">'
                + formInput("Nombre", "famNombre", "text", true, "tag")
                + '</form>';
            var footer = '<button class="btn btn-secondary btn-sm" data-bs-dismiss="modal"><i class="bi bi-x-lg me-1"></i>Cancelar</button>'
                + '<button class="btn btn-primary btn-sm" data-action="familia-do-create"><i class="bi bi-check-lg me-1"></i>Crear</button>';
            showModal("Nueva Familia", body, footer);
        },

        doCreate: function () {
            var data = {
                nombre: getFormValTrimmed("famNombre"),
                descuento: 0
            };
            if (!data.nombre) { showToast("Error", "El nombre es obligatorio", "danger"); return; }
            MexClubApi.createFamilia(data)
                .then(function (res) {
                    if (res.success) {
                        closeModal();
                        showToast("Familia creada", "", "success");
                        Familias.load();
                    }
                })
                .catch(function (err) { showToast("Error", err.message, "danger"); });
        }
    };

    $(document).ready(init);

    return {
        Nav: Nav,
        Socios: Socios,
        Fichaje: Fichaje,
        Articulos: Articulos,
        Familias: Familias,
        Aportaciones: Aportaciones,
        Cuotas: Cuotas,
        Retiradas: Retiradas,
        utils: {
            compressImage: compressImage
        }
    };
})();
