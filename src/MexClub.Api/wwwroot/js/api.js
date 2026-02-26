"use strict";

var MexClubApi = (function () {
    var _baseUrl = (typeof MEXCLUB_CONFIG !== "undefined" && MEXCLUB_CONFIG.API_BASE_URL)
        ? MEXCLUB_CONFIG.API_BASE_URL
        : "/api";
    var _token = null;
    var _refreshToken = null;
    var _user = null;
    var _isRefreshing = false;

    function getHeaders() {
        var headers = { "Content-Type": "application/json" };
        if (_token) headers["Authorization"] = "Bearer " + _token;
        return headers;
    }

    function setAuth(loginResponse) {
        _token = loginResponse.token;
        _refreshToken = loginResponse.refreshToken;
        _user = {
            id: loginResponse.userId,
            username: loginResponse.username,
            role: loginResponse.rol
        };
        sessionStorage.setItem("mexclub_token", _token);
        sessionStorage.setItem("mexclub_refresh", _refreshToken);
        sessionStorage.setItem("mexclub_user", JSON.stringify(_user));
    }

    function loadAuth() {
        _token = sessionStorage.getItem("mexclub_token");
        _refreshToken = sessionStorage.getItem("mexclub_refresh");
        try {
            var u = sessionStorage.getItem("mexclub_user");
            if (u) _user = JSON.parse(u);
        } catch (e) {
            clearAuth();
            return false;
        }
        return !!_token;
    }

    function clearAuth() {
        _token = null;
        _refreshToken = null;
        _user = null;
        sessionStorage.removeItem("mexclub_token");
        sessionStorage.removeItem("mexclub_refresh");
        sessionStorage.removeItem("mexclub_user");
    }

    function buildQuery(params) {
        var parts = [];
        Object.keys(params).forEach(function (key) {
            if (params[key] !== undefined && params[key] !== null) {
                parts.push(encodeURIComponent(key) + "=" + encodeURIComponent(params[key]));
            }
        });
        return parts.length ? "?" + parts.join("&") : "";
    }

    function parseError(xhr) {
        var msg = "Error de conexión";
        try {
            var body = JSON.parse(xhr.responseText);
            msg = body.message || (body.errors && body.errors[0]) || msg;
        } catch (e) { /* respuesta no JSON, mantener msg por defecto */ }
        return { message: msg, status: xhr.status };
    }

    function request(method, endpoint, data) {
        return $.ajax({
            url: _baseUrl + endpoint,
            method: method,
            headers: getHeaders(),
            data: data ? JSON.stringify(data) : undefined,
            dataType: "json"
        }).catch(function (xhr) {
            if (xhr.status === 401 && _refreshToken && !_isRefreshing) {
                return refreshAndRetry(method, endpoint, data);
            }
            return $.Deferred().reject(parseError(xhr)).promise();
        });
    }

    function refreshAndRetry(method, endpoint, data) {
        _isRefreshing = true;
        return $.ajax({
            url: _baseUrl + "/auth/refresh",
            method: "POST",
            headers: { "Content-Type": "application/json" },
            data: JSON.stringify({ token: _token, refreshToken: _refreshToken }),
            dataType: "json"
        }).then(function (res) {
            _isRefreshing = false;
            if (res.success && res.data) {
                setAuth(res.data);
                return request(method, endpoint, data);
            }
            clearAuth();
            location.reload();
        }).catch(function () {
            _isRefreshing = false;
            clearAuth();
            location.reload();
        });
    }

    return {
        loadAuth: loadAuth,
        clearAuth: clearAuth,
        getUser: function () { return _user; },
        isAdmin: function () { return _user && _user.role === "admin"; },

        login: function (username, password) {
            return request("POST", "/auth/login", { username: username, password: password })
                .then(function (res) {
                    if (res.success && res.data) setAuth(res.data);
                    return res;
                });
        },

        logout: function () {
            var payload = { refreshToken: _refreshToken };
            clearAuth();
            return request("POST", "/auth/logout", payload);
        },

        changePassword: function (oldPassword, newPassword) {
            return request("POST", "/auth/change-password", {
                oldPassword: oldPassword,
                newPassword: newPassword
            });
        },

        getSocios: function (page, pageSize, soloActivos) {
            return request("GET", "/socios" + buildQuery({
                page: page || 1,
                pageSize: pageSize || 20,
                soloActivos: soloActivos
            }));
        },

        getSocio: function (id) { return request("GET", "/socios/" + id); },
        buscarSocio: function (code) { return request("GET", "/socios/buscar/" + encodeURIComponent(code)); },
        searchSocios: function (q, limit) { return request("GET", "/socios/search" + buildQuery({ q: q, limit: limit || 30 })); },
        createSocio: function (data) { return request("POST", "/socios", data); },
        updateSocio: function (id, data) { return request("PUT", "/socios/" + id, data); },
        deactivateSocio: function (id) { return request("DELETE", "/socios/" + id); },
        getReferidos: function (codigo) { return request("GET", "/socios/referidos/" + encodeURIComponent(codigo)); },
        uploadSocioImages: function (socioId, formData) {
            if (!_token) loadAuth();
            return $.ajax({
                url: _baseUrl + "/socios/" + socioId + "/upload",
                type: "POST",
                data: formData,
                processData: false,
                contentType: false,
                headers: _token ? { "Authorization": "Bearer " + _token } : {}
            });
        },

        getFamilias: function (soloActivas) {
            return request("GET", "/familias" + buildQuery({ soloActivas: soloActivas }));
        },
        getFamilia: function (id) { return request("GET", "/familias/" + id); },
        createFamilia: function (data) { return request("POST", "/familias", data); },
        updateFamilia: function (id, data) { return request("PUT", "/familias/" + id, data); },
        deactivateFamilia: function (id) { return request("DELETE", "/familias/" + id); },
        countFamiliaArticulos: function (id) { return request("GET", "/familias/" + id + "/articulos-activos"); },

        getArticulos: function (soloActivos, familiaId) {
            return request("GET", "/articulos" + buildQuery({
                soloActivos: soloActivos !== false,
                familiaId: familiaId
            }));
        },
        getArticulo: function (id) { return request("GET", "/articulos/" + id); },
        createArticulo: function (data) { return request("POST", "/articulos", data); },
        updateArticulo: function (id, data) { return request("PUT", "/articulos/" + id, data); },

        getAportaciones: function (page, pageSize, socioId) {
            return request("GET", "/aportaciones" + buildQuery({
                page: page || 1,
                pageSize: pageSize || 20,
                socioId: socioId
            }));
        },
        createAportacion: function (data) { return request("POST", "/aportaciones", data); },
        deleteAportacion: function (id) { return request("DELETE", "/aportaciones/" + id); },

        getAccesos: function (page, pageSize, socioId) {
            return request("GET", "/accesos" + buildQuery({
                page: page || 1,
                pageSize: pageSize || 20,
                socioId: socioId
            }));
        },
        fichar: function (socioId) { return request("POST", "/accesos/fichar", { socioId: socioId }); },

        getCuotas: function (page, pageSize) {
            return request("GET", "/cuotas" + buildQuery({
                page: page || 1,
                pageSize: pageSize || 20
            }));
        },
        getCuotaUltimaPorSocio: function (socioId) { return request("GET", "/cuotas/ultima-por-socio/" + socioId); },
        getSocioUltimaAportacion: function () { return request("GET", "/cuotas/socio-ultima-aportacion"); },
        createCuota: function (data) { return request("POST", "/cuotas", data); },

        getRetiradas: function (page, pageSize, socioId) {
            return request("GET", "/retiradas" + buildQuery({
                page: page || 1,
                pageSize: pageSize || 20,
                socioId: socioId
            }));
        },
        createRetirada: function (data) { return request("POST", "/retiradas", data); },
        createRetiradaBatch: function (data) { return request("POST", "/retiradas/batch", data); },
        deleteRetirada: function (id) { return request("DELETE", "/retiradas/" + id); },

        getDashboard: function () { return request("GET", "/dashboard"); },

        ping: function () { return request("GET", "/ping"); }
    };
})();
