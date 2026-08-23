/* ===========================================================================
   GigApp — global helpers. Loaded on every page.
   Documented in docs/REUSABLE.md — read that instead of this file.
   =========================================================================== */
(function (window, $) {
    'use strict';

    var App = window.App || {};

    /* ---------------------------------------------------------------- config */
    App.masterUrl = function (key) {
        return '/api/masters/' + encodeURIComponent(key) + '?term=';
    };

    /* ----------------------------------------------------------------- toast */
    /**
     * showToast(title, message, cssClass)
     * cssClass: 'bg-danger' | 'bg-success' | 'bg-warning' | 'bg-primary'
     */
    function showToast(title, message, cssClass) {
        var $host = $('#toast-host');
        if (!$host.length) {
            $host = $('<div id="toast-host" class="toast-container position-fixed top-0 end-0 p-3"></div>')
                .appendTo(document.body);
        }

        var $toast = $(
            '<div class="toast align-items-center text-white ' + (cssClass || 'bg-primary') + ' border-0" ' +
            'role="alert" aria-live="assertive" aria-atomic="true">' +
              '<div class="d-flex">' +
                '<div class="toast-body"><strong></strong><span></span></div>' +
                '<button type="button" class="btn-close btn-close-white me-2 m-auto" ' +
                'data-bs-dismiss="toast" aria-label="Close"></button>' +
              '</div>' +
            '</div>');

        $toast.find('strong').text(title ? title + ' ' : '');
        $toast.find('span').text(message || '');
        $host.append($toast);

        // bootstrap is loaded globally; fall back to a plain timeout if not.
        if (window.bootstrap && window.bootstrap.Toast) {
            var t = new window.bootstrap.Toast($toast[0], { delay: 4000 });
            t.show();
            $toast.on('hidden.bs.toast', function () { $toast.remove(); });
        } else {
            $toast.addClass('show');
            window.setTimeout(function () { $toast.remove(); }, 4000);
        }
    }

    /* ---------------------------------------------------------- autocomplete */
    /**
     * AutoComplete(inputId, hiddenId, url, alertMsg, onSelect)
     *
     * Binds a visible text input to a hidden id input. The text box holds the
     * NAME (so the user can type), the hidden field holds the ID (what actually
     * gets posted). A name that is not picked from the list leaves the hidden
     * field empty, so the server rejects it — a typo can never invent a record.
     *
     * `url` must return { success: true, data: [{ id, name, hint }] } and must
     * already end with the query key, e.g. App.masterUrl('skill-category').
     */
    function toSuggestions(items) {
        return (items || []).map(function (item) {
            // A bare "-" would render as an empty row, so pad it.
            var label = item.name === '-' ? '​-' : item.name;
            return { label: label, value: label, id: String(item.id), raw: item };
        });
    }

    /**
     * Remote source: hits a master endpoint on every keystroke.
     * `getParentId` is optional — dependent masters (service items inside a
     * category) read it fresh on each call so changing the parent takes effect
     * immediately.
     */
    function remoteSource(url, getParentId) {
        return function (request, response) {
            var full = url + encodeURIComponent(request.term);

            if (typeof getParentId === 'function') {
                var parentId = getParentId();
                if (!parentId) { response([]); return; }   // no parent, nothing to offer
                full += '&parentId=' + encodeURIComponent(parentId);
            }

            fetch(full, {
                headers: { 'Accept': 'application/json' },
                credentials: 'same-origin'
            })
                .then(function (res) { return res.ok ? res.json() : null; })
                .then(function (result) {
                    response(toSuggestions(result && result.success ? result.data : []));
                })
                .catch(function () { response([]); });
        };
    }

    /**
     * Local source: filters a list already on the page. Used by anonymous pages
     * (registration) where the master API is not reachable without a token, so
     * the options are server-rendered instead of fetched.
     */
    function localSource(items) {
        return function (request, response) {
            var term = (request.term || '').toLowerCase();
            response(toSuggestions(items.filter(function (item) {
                return !term || item.name.toLowerCase().indexOf(term) !== -1;
            })));
        };
    }

    function AutoComplete(inputId, hiddenId, source, alertMsg, onSelect) {
        var $input = $('#' + inputId);
        var $hidden = $('#' + hiddenId);

        if (!$input.length || !$hidden.length) return;

        // A string source is a URL; anything else is already a source function.
        var sourceFn = typeof source === 'string' ? remoteSource(source) : source;

        // Stops the browser's own dropdown covering the jQuery UI one.
        $input.attr('autocomplete', 'new-password');

        $input.autocomplete({
            minLength: 0,
            delay: 200,
            source: sourceFn,

            open: function () {
                $input.autocomplete('widget').css({
                    'z-index': 99999,
                    'min-width': $input.outerWidth() + 'px',
                    'max-width': '320px',
                    'max-height': '250px',
                    'overflow-y': 'auto',
                    'overflow-x': 'hidden'
                });
            },

            focus: function (event) { event.preventDefault(); },

            select: function (event, ui) {
                $input.val(ui.item.label.replace('​', ''));
                $hidden.val(ui.item.id).trigger('change');
                if (typeof onSelect === 'function') onSelect(ui.item.raw);
                return false;
            }
        });

        // Two-line row: name on top, hint below.
        $input.data('ui-autocomplete')._renderItem = function (ul, item) {
            var $row = $('<div>').addClass('ac-row').text(item.label);
            if (item.raw && item.raw.hint) {
                $row.append($('<small>').addClass('ac-hint d-block text-muted').text(item.raw.hint));
            }
            return $('<li>').data('ui-autocomplete-item', item).append($row).appendTo(ul);
        };

        // Empty search on focus = show the first page of options.
        $input.on('focus', function () { $(this).autocomplete('search', ''); });

        // Typing by hand invalidates the previous pick.
        $input.on('input', function () { $hidden.val(''); });

        $input.on('blur', function () {
            if ($input.val() === '') { $hidden.val(''); return; }
            if (!$hidden.val()) clearAndAlert();
        });

        // TAB should commit whatever row is highlighted.
        $input.on('keydown', function (e) {
            if (e.which !== 9) return;

            var active = $input.autocomplete('instance').menu.active;
            if (!active) return;

            var item = active.data('ui-autocomplete-item');
            if (!item) return;

            $input.val(item.label.replace('​', ''));
            $hidden.val(item.id).trigger('change');
            if (typeof onSelect === 'function') onSelect(item.raw);
        });

        function clearAndAlert() {
            $input.val('');
            $hidden.val('');
            if (alertMsg) showToast('Error', 'Invalid ' + alertMsg, 'bg-danger');
        }
    }

    /**
     * Shorthand for a master-backed picker:
     *   App.masterPicker('CategoryPicker', 'CategoryId', 'skill-category', 'category');
     */
    function masterPicker(inputId, hiddenId, masterKey, alertMsg, onSelect, parentFieldId) {
        var source = parentFieldId
            ? remoteSource(App.masterUrl(masterKey), function () { return $('#' + parentFieldId).val(); })
            : App.masterUrl(masterKey);

        AutoComplete(inputId, hiddenId, source, alertMsg, onSelect);

        // Changing the parent invalidates whatever child was chosen — clear it
        // rather than leave a service item pointing at the wrong category.
        if (parentFieldId) {
            $('#' + parentFieldId).on('change', function () {
                $('#' + inputId).val('');
                $('#' + hiddenId).val('');
            });
        }
    }

    /**
     * Picker backed by options already on the page instead of the master API.
     *   App.localPicker('SkillPicker', 'SkillCategoryId', 'skill-options', 'skill category');
     * `listId` is a <datalist> whose options carry data-id.
     */
    function localPicker(inputId, hiddenId, listId, alertMsg, onSelect) {
        var items = $('#' + listId + ' option').map(function () {
            return {
                id: $(this).data('id'),
                name: $(this).attr('value'),
                hint: $(this).data('hint') || null
            };
        }).get();

        AutoComplete(inputId, hiddenId, localSource(items), alertMsg, onSelect);
    }

    /* -------------------------------------------------------------- helpers */

    /**
     * "Use my current location" buttons.
     *
     * Fills a pair of hidden lat/lon fields from the browser's geolocation.
     * Both are written together or neither is — the server rejects half a pin,
     * because an address with only a latitude looks mappable but is not.
     *
     *   <button data-capture-location data-lat="Latitude" data-lon="Longitude"
     *           data-status="pinStatus">Use my location</button>
     */
    function wireLocationCapture() {
        $(document).on('click', '[data-capture-location]', function (e) {
            e.preventDefault();

            var $btn = $(this);
            var $lat = $('#' + $btn.data('lat'));
            var $lon = $('#' + $btn.data('lon'));
            var $status = $btn.data('status') ? $('#' + $btn.data('status')) : $();

            if (!navigator.geolocation) {
                showToast('Error', 'This browser cannot share a location.', 'bg-danger');
                return;
            }

            $btn.prop('disabled', true);
            $status.text('Finding your location…').removeClass('text-danger text-success');

            navigator.geolocation.getCurrentPosition(
                function (pos) {
                    $lat.val(pos.coords.latitude.toFixed(6));
                    $lon.val(pos.coords.longitude.toFixed(6));
                    $status.text('Location captured (accurate to about '
                        + Math.round(pos.coords.accuracy) + ' m)').addClass('text-success');
                    $btn.prop('disabled', false);
                },
                function (err) {
                    // Leave both fields untouched — a partial pin is worse than none.
                    $status.text(err.code === err.PERMISSION_DENIED
                        ? 'Location permission was denied. You can still save the address without a pin.'
                        : 'Could not get your location. You can still save the address without a pin.')
                        .addClass('text-danger');
                    $btn.prop('disabled', false);
                },
                { enableHighAccuracy: true, timeout: 10000, maximumAge: 0 });
        });
    }

    /** Confirm before a destructive submit. Use data-confirm="Are you sure?". */
    function wireConfirms() {
        $(document).on('submit', 'form[data-confirm]', function (e) {
            if (!window.confirm($(this).data('confirm'))) e.preventDefault();
        });
    }

    /** Auto-submit a filter form when a select changes. Use data-auto-submit. */
    function wireAutoSubmit() {
        $(document).on('change', '[data-auto-submit] select', function () {
            $(this).closest('form').submit();
        });
    }

    /**
     * Declarative pickers:
     *   data-master="key"      → fetches from /api/masters/{key} (needs a session)
     *   data-options="listId"  → filters a server-rendered <datalist> (works anonymous)
     * Both need data-target="hiddenFieldId".
     */
    function wireDeclarativePickers() {
        $('input[data-master][data-target]').each(function () {
            var $el = $(this);
            masterPicker($el.attr('id'), $el.data('target'), $el.data('master'),
                         $el.data('alert'), null, $el.data('parent'));
        });

        $('input[data-options][data-target]').each(function () {
            var $el = $(this);
            localPicker($el.attr('id'), $el.data('target'), $el.data('options'), $el.data('alert'));
        });
    }

    App.showToast = showToast;
    App.AutoComplete = AutoComplete;
    App.masterPicker = masterPicker;
    App.localPicker = localPicker;
    window.App = App;
    window.showToast = showToast;      // AutoComplete's error path calls this bare
    window.AutoComplete = AutoComplete;

    $(function () {
        wireConfirms();
        wireAutoSubmit();
        wireDeclarativePickers();
        wireLocationCapture();
    });

})(window, jQuery);
