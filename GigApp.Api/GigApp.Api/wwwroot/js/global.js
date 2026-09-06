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
        'bg-primary': 'info',
        'danger': 'error',
        'success': 'success',
        'warning': 'warning',
        'info': 'info'
    };

    function swal() {
        return window.Swal;
    }

    function missing(helper) {
        if (window.console) {
            window.console.error('SweetAlert2 did not load, so App.' + helper + ' cannot run.');
        }
    }

    var TOASTS = {
        success: { title: 'Success', icon: '&#10003;' },
        warning: { title: 'Warning', icon: '&#33;' },
        error: { title: 'Error', icon: '&#10007;' },
        info: { title: 'Info', icon: '&#105;' }
    };

    function toastHost() {
        var host = document.getElementById('toast-host');

        if (!host) {
            host = document.createElement('div');
            host.id = 'toast-host';
            host.className = 'gig-toast-host';
            document.body.appendChild(host);
        }

        return host;
    }

    function showToast(title, message, kind) {
        var type = ICONS[kind] || (TOASTS[kind] ? kind : 'info');
        var preset = TOASTS[type];

        var heading = title || preset.title;
        var body = message === undefined || message === null ? '' : String(message);

        // Called as showToast('Saved') — the one argument is the message.
        if (!message && title && !TOASTS[kind]) {
            heading = preset.title;
            body = title;
        }

        var toast = document.createElement('div');
        toast.className = 'gig-toast gig-toast-' + type;
        toast.setAttribute('role', 'status');
        toast.innerHTML =
            '<span class="gig-toast-icon">' + preset.icon + '</span>' +
            '<div class="gig-toast-body">' +
                '<div class="gig-toast-title"></div>' +
                '<div class="gig-toast-text"></div>' +
            '</div>' +
            '<button type="button" class="gig-toast-close" aria-label="Close">&times;</button>' +
            '<span class="gig-toast-bar"></span>';

        toast.querySelector('.gig-toast-title').textContent = heading;
        toast.querySelector('.gig-toast-text').textContent = body;

        toastHost().appendChild(toast);

        var timer = window.setTimeout(dismiss, 4500);
        toast.querySelector('.gig-toast-close').addEventListener('click', function () {
            window.clearTimeout(timer);
            dismiss();
        });

        function dismiss() {
            toast.classList.add('gig-toast-leaving');
            window.setTimeout(function () { toast.remove(); }, 250);
        }
    }

    App.toastSuccess = function (message, title) { showToast(title, message, 'success'); };
    App.toastError = function (message, title) { showToast(title, message, 'error'); };
    App.toastWarning = function (message, title) { showToast(title, message, 'warning'); };
    App.toastInfo = function (message, title) { showToast(title, message, 'info'); };

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
                $input.trigger('master:selected', [ui.item.raw]);
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

        $input.on('input', function () {
            $hidden.val('');
            $input.trigger('master:cleared');
        });

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
            $input.trigger('master:selected', [item.raw]);
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
                $('#' + inputId).val('').trigger('master:cleared');
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

            askThenSubmit($form, $form, $form.data('submitter'));
        });

        $(document).on('click', 'form [type="submit"]', function (e) {
            var $btn = $(this);
            var $form = $btn.closest('form');
            var submitter = { name: this.name, value: this.value };

            $form.data('submitter', submitter);

            // A confirm on the button itself wins over one on the form, so a
            // single form can ask about one button and not the others.
            if (!$btn.data('confirm')) return;

            e.preventDefault();
            askThenSubmit($form, $btn, submitter);
        });
    }

    function askThenSubmit($form, $source, submitter) {
        confirmAction({
            title: $source.data('confirm-title'),
            text: $source.data('confirm'),
            icon: $source.data('confirm-icon'),
            confirmText: $source.data('confirm-ok')
        }).then(function (ok) {
            if (!ok) return;

            $form.data('confirmed', true);
            $form.removeData('submitter');

            if (submitter && submitter.name) {
                $('<input>').attr({ type: 'hidden', name: submitter.name, value: submitter.value })
                    .appendTo($form);
            }

            $form[0].submit();
        });
    }

    function wireFlash() {
        $('[data-flash]').each(function () {
            var $el = $(this);
            var success = $el.data('flash-success');
            var error = $el.data('flash-error');

            if (success) showToast(null, success, 'success');
            if (error) showToast(null, error, 'error');

            $el.remove();
        });
    }

    function wireAutoSubmit() {
        $(document).on('change', '[data-auto-submit] select', function () {
            $(this).closest('form').submit();
        });
    }

    function applyFixedPrice($picker, price) {
        var $field = $('#' + $picker.data('price-field'));
        if (!$field.length) return;

        var $note = $('#' + $picker.data('price-note'));
        var fixed = price !== null && price !== undefined && price !== '';

        $field.prop('readonly', fixed).toggleClass('bg-body-secondary', fixed);
        if (fixed) $field.val(price);

        if ($note.length) {
            $note.text(fixed ? $picker.data('price-fixed-note') : $note.data('default'));
        }
    }

    function wireFixedPrice() {
        $('input[data-price-field]').each(function () {
            var $note = $('#' + $(this).data('price-note'));
            if ($note.length) $note.data('default', $note.text());
        });

        $(document).on('master:selected', 'input[data-price-field]', function (event, item) {
            applyFixedPrice($(this), item && item.extra ? item.extra.fixedPrice : null);
        });

        $(document).on('master:cleared', 'input[data-price-field]', function () {
            applyFixedPrice($(this), null);
        });
    }

    var IMAGE_TYPES = ['image/jpeg', 'image/png', 'image/webp'];

    function readableSize(bytes) {
        return bytes >= 1048576
            ? (bytes / 1048576).toFixed(1) + ' MB'
            : Math.max(1, Math.round(bytes / 1024)) + ' KB';
    }

    function shrinkImage(file, maxPixels, quality) {
        if (!window.createImageBitmap || !window.HTMLCanvasElement) {
            return Promise.resolve(null);
        }

        return createImageBitmap(file, { imageOrientation: 'from-image' })
            .then(function (bitmap) {
                var scale = Math.min(1, maxPixels / Math.max(bitmap.width, bitmap.height));
                var canvas = document.createElement('canvas');
                canvas.width = Math.round(bitmap.width * scale);
                canvas.height = Math.round(bitmap.height * scale);

                var context = canvas.getContext('2d');
                // A transparent PNG would otherwise composite onto black.
                context.fillStyle = '#ffffff';
                context.fillRect(0, 0, canvas.width, canvas.height);
                context.drawImage(bitmap, 0, 0, canvas.width, canvas.height);
                bitmap.close();

                return new Promise(function (resolve) {
                    canvas.toBlob(function (blob) { resolve(blob); }, 'image/jpeg', quality);
                });
            })
            .catch(function () { return null; });
    }

    function setupImageUpload($box) {
        var input = $box.find('input.image-file')[0];
        if (!input) return;

        var $drop = $box.find('[data-image-drop]');
        var $figure = $box.find('[data-image-figure]');
        var $thumb = $box.find('[data-image-thumb]');
        var $empty = $box.find('[data-image-empty]');
        var $status = $box.find('[data-image-status]');

        var maxBytes = parseInt($box.data('max-bytes'), 10) || 5242880;
        var maxPixels = parseInt($box.data('max-pixels'), 10) || 1600;
        var hint = $status.text();
        var objectUrl = null;
        var required = input.required;

        function say(message, isError) {
            $status.text(message || hint).toggleClass('is-error', !!isError);
        }

        function show(file) {
            if (objectUrl) URL.revokeObjectURL(objectUrl);
            objectUrl = URL.createObjectURL(file);
            $thumb.attr('src', objectUrl);
            $figure.prop('hidden', false);
            $empty.prop('hidden', true);
        }

        function clear() {
            if (objectUrl) { URL.revokeObjectURL(objectUrl); objectUrl = null; }
            input.value = '';
            input.required = required;
            $thumb.attr('src', '');
            $figure.prop('hidden', true);
            $empty.prop('hidden', false);
            say(null, false);
        }

        function put(file) {
            var transfer = new DataTransfer();
            transfer.items.add(file);
            input.files = transfer.files;
            // A replacement satisfies the field even when the original is gone.
            input.required = false;
        }

        function reject(reason, toast) {
            input.value = '';
            input.required = required;
            say(reason, true);
            showToast('Error', toast, 'bg-danger');
        }

        function accept(file) {
            if (!file) return;

            if (IMAGE_TYPES.indexOf(file.type) === -1) {
                reject('Choose a JPG, PNG or WEBP image.',
                       'Only JPG, PNG and WEBP images are allowed.');
                return;
            }

            $drop.addClass('is-busy');
            say('Preparing…', false);

            shrinkImage(file, maxPixels, 0.85).then(function (blob) {
                $drop.removeClass('is-busy');

                var useSmaller = blob && blob.size < file.size;
                var finalFile = file;

                if (useSmaller) {
                    var base = file.name.replace(/\.[^.]+$/, '') || 'image';
                    finalFile = new File([blob], base + '.jpg', { type: 'image/jpeg' });
                }

                if (finalFile.size > maxBytes) {
                    reject('That image is ' + readableSize(finalFile.size)
                           + '. The limit is ' + readableSize(maxBytes) + '.',
                           'That image is too large to upload.');
                    return;
                }

                put(finalFile);
                show(finalFile);

                say(useSmaller
                    ? 'Ready — resized from ' + readableSize(file.size) + ' to ' + readableSize(finalFile.size) + '.'
                    : 'Ready — ' + readableSize(finalFile.size) + '.', false);
            });
        }

        $(input).on('change', function () {
            if (input.files && input.files.length) accept(input.files[0]);
        });

        $drop.on('dragover dragenter', function (event) {
            event.preventDefault();
            $drop.addClass('is-dragging');
        });

        $drop.on('dragleave drop', function () { $drop.removeClass('is-dragging'); });

        $drop.on('drop', function (event) {
            event.preventDefault();
            var dropped = event.originalEvent.dataTransfer;
            if (dropped && dropped.files && dropped.files.length) accept(dropped.files[0]);
        });

        $drop.on('keydown', function (event) {
            if (event.which === 13 || event.which === 32) {
                event.preventDefault();
                input.click();
            }
        });

        $drop.on('paste', function (event) {
            var items = (event.originalEvent.clipboardData || {}).items || [];
            for (var i = 0; i < items.length; i++) {
                if (items[i].kind === 'file') {
                    event.preventDefault();
                    accept(items[i].getAsFile());
                    return;
                }
            }
        });

        $box.find('[data-image-clear]').on('click', function (event) {
            event.preventDefault();
            event.stopPropagation();
            clear();
        });
    }

    function wireLatLonCopy() {
        $(document).on('change', 'select[data-copy-latlon]', function () {
            var $select = $(this);
            var $option = $select.find('option:selected');
            if (!$option.val()) return;

            var pairs = [
                [$select.data('lat'), $option.data('lat')],
                [$select.data('lon'), $option.data('lon')],
                [$select.data('city'), $option.data('city')],
                [$select.data('pincode'), $option.data('pincode')]
            ];

            pairs.forEach(function (pair) {
                if (pair[0]) $('#' + pair[0]).val(pair[1] === undefined ? '' : pair[1]);
            });

            showToast('Success', 'Copied from ' + $option.text().trim() + '.', 'bg-success');
        });
    }

    function wireImageUploads() {
        $('[data-image-upload]').each(function () { setupImageUpload($(this)); });
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
    App.wireImageUploads = wireImageUploads;
    window.App = App;
    window.showToast = showToast;
    window.AutoComplete = AutoComplete;

    $(function () {
        wireConfirms();
        wireFlash();
        wireAutoSubmit();
        wireFixedPrice();
        wireImageUploads();
        wireLatLonCopy();
        wireDeclarativePickers();
        wireLocationCapture();
    });

})(window, jQuery);
