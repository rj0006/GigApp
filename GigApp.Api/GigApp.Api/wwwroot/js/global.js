(function (window, $) {
    'use strict';

    var App = window.App || {};

    App.masterUrl = function (key) {
        return '/api/masters/' + encodeURIComponent(key) + '?term=';
    };

    var ICONS = {
        'bg-danger': 'error',
        'bg-warning': 'warning',
        'bg-success': 'success',
        'bg-primary': 'info'
    };

    function swal() {
        return window.Swal;
    }

    function missing(helper) {
        if (window.console) {
            window.console.error('SweetAlert2 did not load, so App.' + helper + ' cannot run.');
        }
    }

    function showToast(title, message, cssClass) {
        var text = [title, message].filter(Boolean).join(' ').trim();

        if (!swal()) {
            missing('showToast');
            return;
        }

        swal().fire({
            toast: true,
            position: 'top-end',
            icon: ICONS[cssClass] || 'info',
            title: text,
            showConfirmButton: false,
            timer: 4000,
            timerProgressBar: true
        });
    }

    function confirmAction(options) {
        var settings = typeof options === 'string' ? { text: options } : (options || {});

        // Refusing is the safe answer — never fall back to a browser dialog.
        if (!swal()) {
            missing('confirmAction');
            return Promise.resolve(false);
        }

        return swal().fire({
            title: settings.title || 'Please confirm',
            text: settings.text || 'Are you sure?',
            icon: settings.icon || 'warning',
            showCancelButton: true,
            confirmButtonText: settings.confirmText || 'Yes, continue',
            cancelButtonText: settings.cancelText || 'Cancel',
            confirmButtonColor: settings.icon === 'question' ? '#0d6efd' : '#dc3545',
            cancelButtonColor: '#6c757d',
            reverseButtons: true,
            focusCancel: true
        }).then(function (result) { return result.isConfirmed === true; });
    }

    function notify(options) {
        var settings = typeof options === 'string' ? { text: options } : (options || {});

        if (!swal()) {
            missing('notify');
            return Promise.resolve();
        }

        return swal().fire({
            title: settings.title || '',
            text: settings.text || '',
            icon: settings.icon || 'info',
            confirmButtonText: settings.confirmText || 'OK',
            confirmButtonColor: '#0d6efd'
        });
    }

    function toSuggestions(items) {
        return (items || []).map(function (item) {
            var label = item.name === '-' ? '​-' : item.name;
            return { label: label, value: label, id: String(item.id), raw: item };
        });
    }

    function remoteSource(url, getParentId) {
        return function (request, response) {
            var full = url + encodeURIComponent(request.term);

            if (typeof getParentId === 'function') {
                var parentId = getParentId();
                if (!parentId) { response([]); return; }
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

        var sourceFn = typeof source === 'string' ? remoteSource(source) : source;

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

        $input.data('ui-autocomplete')._renderItem = function (ul, item) {
            var $row = $('<div>').addClass('ac-row').text(item.label);
            if (item.raw && item.raw.hint) {
                $row.append($('<small>').addClass('ac-hint d-block text-muted').text(item.raw.hint));
            }
            return $('<li>').data('ui-autocomplete-item', item).append($row).appendTo(ul);
        };

        $input.on('focus', function () { $(this).autocomplete('search', ''); });

        $input.on('input', function () { $hidden.val(''); });

        $input.on('blur', function () {
            if ($input.val() === '') { $hidden.val(''); return; }
            if (!$hidden.val()) clearAndAlert();
        });

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

    function masterPicker(inputId, hiddenId, masterKey, alertMsg, onSelect, parentFieldId) {
        var source = parentFieldId
            ? remoteSource(App.masterUrl(masterKey), function () { return $('#' + parentFieldId).val(); })
            : App.masterUrl(masterKey);

        AutoComplete(inputId, hiddenId, source, alertMsg, onSelect);

        if (parentFieldId) {
            $('#' + parentFieldId).on('change', function () {
                $('#' + inputId).val('');
                $('#' + hiddenId).val('');
            });
        }
    }

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
                    $status.text(err.code === err.PERMISSION_DENIED
                        ? 'Location permission was denied. You can still save the address without a pin.'
                        : 'Could not get your location. You can still save the address without a pin.')
                        .addClass('text-danger');
                    $btn.prop('disabled', false);
                },
                { enableHighAccuracy: true, timeout: 10000, maximumAge: 0 });
        });
    }

    function wireConfirms() {
        // A hidden input reports defaultValue as whatever value currently holds,
        // so the starting value has to be snapshotted here instead.
        $('form[data-confirm-changed]').each(function () {
            var $form = $(this);
            var field = document.getElementById($form.data('confirm-changed'));
            if (field) $form.data('confirm-original', field.value);
        });

        $(document).on('submit', 'form[data-confirm]', function (e) {
            var form = this;
            var $form = $(form);

            if ($form.data('confirmed')) {
                $form.removeData('confirmed');
                return;
            }

            var watch = $form.data('confirm-changed');
            if (watch) {
                var field = document.getElementById(watch);
                if (field && field.value === $form.data('confirm-original')) return;
            }

            e.preventDefault();

            confirmAction({
                title: $form.data('confirm-title'),
                text: $form.data('confirm'),
                icon: $form.data('confirm-icon'),
                confirmText: $form.data('confirm-ok')
            }).then(function (ok) {
                if (!ok) return;
                $form.data('confirmed', true);

                var submitter = $form.data('submitter');
                $form.removeData('submitter');

                if (submitter && submitter.name) {
                    $('<input>').attr({ type: 'hidden', name: submitter.name, value: submitter.value })
                        .appendTo($form);
                }

                form.submit();
            });
        });

        $(document).on('click', 'form[data-confirm] [type="submit"]', function () {
            $(this).closest('form').data('submitter', { name: this.name, value: this.value });
        });
    }

    function wireAutoSubmit() {
        $(document).on('change', '[data-auto-submit] select', function () {
            $(this).closest('form').submit();
        });
    }

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
    App.confirmAction = confirmAction;
    App.notify = notify;
    App.AutoComplete = AutoComplete;
    App.masterPicker = masterPicker;
    App.localPicker = localPicker;
    window.App = App;
    window.showToast = showToast;
    window.AutoComplete = AutoComplete;

    $(function () {
        wireConfirms();
        wireAutoSubmit();
        wireDeclarativePickers();
        wireLocationCapture();
    });

})(window, jQuery);
