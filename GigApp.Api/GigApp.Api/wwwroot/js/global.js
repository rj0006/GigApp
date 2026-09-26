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

    function apiErrorMessage(response, body) {
        if (body && typeof body === 'object') {
            if (body.title) return body.title;
            if (body.error) return body.error;

            if (body.errors) {
                var firstKey = Object.keys(body.errors)[0];
                var firstValue = firstKey && body.errors[firstKey];
                if (Array.isArray(firstValue) && firstValue.length) return firstValue[0];
            }
        }

        return response.statusText || 'Something went wrong.';
    }

    function apiRequest(method, url, data, opts) {
        var silent = opts && opts.silent;

        var options = {
            method: method,
            credentials: 'same-origin',
            headers: { Accept: 'application/json' },
        };

        if (data instanceof FormData) {
            options.body = data;
        } else if (data !== undefined) {
            options.headers['Content-Type'] = 'application/json';
            options.body = JSON.stringify(data);
        }

        return fetch(url, options).then(function (response) {
            return response.text().then(function (text) {
                var body = null;

                if (text) {
                    try { body = JSON.parse(text); } catch (parseFailure) { body = null; }
                }

                if (!response.ok) {
                    var message = apiErrorMessage(response, body);
                    if (!silent) showToast('Error', message, 'error');
                    throw new Error(message);
                }

                return body;
            });
        });
    }

    App.api = {
        get: function (url, opts) { return apiRequest('GET', url, undefined, opts); },
        post: function (url, data, opts) { return apiRequest('POST', url, data, opts); },
        put: function (url, data, opts) { return apiRequest('PUT', url, data, opts); },
        del: function (url, opts) { return apiRequest('DELETE', url, undefined, opts); },
    };

    var Admin = window.Admin || {};

    Admin.escapeHtml = function (text) {
        return $('<div>').text(text == null ? '' : text).html();
    };

    Admin.thumb = function (url) {
        return url
            ? '<img class="master-thumb" src="' + url + '" alt="" loading="lazy" />'
            : '<span class="master-thumb-empty">none</span>';
    };

    Admin.showImagePreview = function (url, fieldName) {
        if (!url) return;
        var $scope = fieldName
            ? $('[data-image-upload]').has('input[name="' + fieldName + '"]')
            : $(document);
        $scope.find('[data-image-thumb]').attr('src', url);
        $scope.find('[data-image-figure]').prop('hidden', false);
        $scope.find('[data-image-empty]').prop('hidden', true);
    };

    Admin.showFormErrors = function (containerSelector, message) {
        $(containerSelector).html(
            '<div class="alert alert-danger small">' + $('<div>').text(message).html() + '</div>');
    };

    Admin.formToJson = function (form) {
        var data = {};
        $(form).serializeArray().forEach(function (field) { data[field.name] = field.value; });
        $(form).find('input[type="checkbox"]').each(function () { data[this.name] = this.checked; });
        return data;
    };

    Admin.kycReviewHtml = function (partner) {
        function doc(url, label) {
            return url
                ? '<a href="' + url + '" target="_blank" rel="noopener noreferrer">'
                  + '<img src="' + url + '" alt="' + label + '" class="img-fluid rounded border" style="max-height:200px" /></a>'
                : '<div class="border rounded d-flex align-items-center justify-content-center text-secondary small" style="height:140px">Not submitted</div>';
        }

        var html = '<dl class="row mb-3 small">'
            + '<dt class="col-sm-4">Partner ID</dt><dd class="col-sm-8">' + partner.id + '</dd>'
            + '<dt class="col-sm-4">Mobile</dt><dd class="col-sm-8 mono">' + Admin.escapeHtml(partner.phone) + '</dd>'
            + '<dt class="col-sm-4">Email</dt><dd class="col-sm-8">' + Admin.escapeHtml(partner.email || '—') + '</dd>'
            + '<dt class="col-sm-4">Phone verified</dt><dd class="col-sm-8">' + (partner.isPhoneVerified ? 'Yes' : 'No') + '</dd>'
            + '<dt class="col-sm-4">Skill category</dt><dd class="col-sm-8">' + Admin.escapeHtml(partner.skillCategoryName) + '</dd>'
            + '<dt class="col-sm-4">Availability</dt><dd class="col-sm-8">' + (partner.isAvailable ? 'On duty' : 'Off duty') + '</dd>'
            + '<dt class="col-sm-4">Registered</dt><dd class="col-sm-8">' + new Date(partner.createdAt).toLocaleString('en-GB') + ' UTC</dd>'
            + '</dl>';

        if (partner.kycReviewNote) {
            html += '<div class="alert alert-primary small"><strong>Why this is in the queue:</strong> '
                + Admin.escapeHtml(partner.kycReviewNote) + '</div>';
        }

        html += '<h6 class="small fw-semibold">KYC documents</h6>'
            + '<p class="text-secondary small">Check that the selfie matches the photo on the Aadhaar card, '
            + 'and that the number below matches the card. Click any image to open it full size.</p>'
            + '<div class="row g-3">'
            + '<div class="col-md-4"><div class="small fw-semibold mb-1">Selfie</div>' + doc(partner.selfieUrl, 'Partner selfie') + '</div>'
            + '<div class="col-md-4"><div class="small fw-semibold mb-1">Aadhaar — front</div>' + doc(partner.aadhaarFrontUrl, 'Aadhaar front') + '</div>'
            + '<div class="col-md-4"><div class="small fw-semibold mb-1">Aadhaar — back</div>' + doc(partner.aadhaarBackUrl, 'Aadhaar back') + '</div>'
            + '<div class="col-12"><span class="small fw-semibold">Aadhaar number:</span> '
            + (partner.aadhaarNumber
                ? '<span class="mono">' + Admin.escapeHtml(partner.aadhaarNumber) + '</span>'
                : '<span class="text-secondary small">Not provided</span>')
            + '</div></div>';

        if (partner.isRejected) {
            html += '<div class="alert alert-danger small mt-3 mb-0"><strong>You rejected this partner</strong>'
                + (partner.kycReviewedAt ? ' on ' + new Date(partner.kycReviewedAt).toLocaleString('en-GB') + ' UTC' : '') + '.'
                + '<div>Reason given: ' + Admin.escapeHtml(partner.kycRejectionReason || '') + '</div>'
                + '<div class="mt-1">They can fix it and resubmit, which brings them back into this queue.</div></div>';
        }

        if (!partner.hasCompleteKyc) {
            html += '<div class="alert alert-warning small mt-3 mb-0">'
                + 'Documents are incomplete, so this partner cannot be approved yet.</div>';
        }

        return html;
    };

    Admin.kycHistoryHtml = function (entries) {
        if (!entries.length) {
            return '<p class="text-center text-secondary py-4 mb-0">No KYC activity has been recorded for this partner yet.</p>';
        }

        var rows = entries.map(function (entry) {
            var detail = entry.detail ? '<div>' + Admin.escapeHtml(entry.detail) + '</div>' : '';
            var remark = (entry.remark && entry.remark !== entry.detail)
                ? '<div class="text-secondary">' + Admin.escapeHtml(entry.remark) + '</div>' : '';
            var empty = (!entry.detail && !entry.remark) ? '<span class="text-secondary">—</span>' : '';
            var at = new Date(entry.at);

            return '<tr>'
                + '<td class="small text-secondary text-nowrap">'
                + at.toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' })
                + '<br /><span class="text-body-tertiary">'
                + at.toLocaleTimeString('en-GB', { hour: '2-digit', minute: '2-digit', hour12: false }) + ' UTC</span></td>'
                + '<td class="fw-semibold small">' + Admin.escapeHtml(entry.action) + '</td>'
                + '<td class="small">' + detail + remark + empty + '</td>'
                + '<td class="small text-secondary">' + Admin.escapeHtml(entry.by || '—') + '</td>'
                + '<td><span class="badge ' + entry.badgeClass + '">' + Admin.escapeHtml(entry.statusLabel) + '</span></td>'
                + '</tr>';
        }).join('');

        return '<p class="text-secondary small">Every KYC event for this partner, newest first. Taken from the '
            + 'audit trail, so it covers registration, every document upload, every skill change and every decision.</p>'
            + '<div class="table-responsive"><table class="table table-sm align-middle mb-0"><thead><tr>'
            + '<th>When</th><th>Action</th><th>Detail</th><th>By</th><th>Status after</th></tr></thead>'
            + '<tbody>' + rows + '</tbody></table></div>';
    };

    Admin.openKycModal = function (partner, history, onDecision) {
        $('#kyc-modal-name').text(partner.name);
        $('#kyc-modal-status').attr('class', 'badge ms-1 ' + partner.kycBadgeClass).text(partner.kycLabel);
        $('#kyc-modal-review').html(Admin.kycReviewHtml(partner));
        $('#kyc-modal-history').html(Admin.kycHistoryHtml(history));
        $('#kyc-modal-history-count').text(history.length);
        $('#kyc-modal-reason').val('');

        $('#kyc-modal-approve').prop('disabled', !partner.hasCompleteKyc);
        $('#kyc-modal-reject').text(partner.isRejected ? 'Reject again' : 'Reject');

        $('#kyc-modal-approve').off('click').on('click', function () {
            var proceed = partner.isRejected
                ? App.confirmAction({
                    title: 'Approve a rejected partner?',
                    text: 'You rejected this partner earlier for: "' + partner.kycRejectionReason
                        + '". Do you want to approve the KYC now?',
                    icon: 'question',
                    confirmText: 'Yes, approve',
                })
                : Promise.resolve(true);

            proceed.then(function (ok) {
                if (ok) onDecision(true, $('#kyc-modal-reason').val());
            });
        });

        $('#kyc-modal-reject').off('click').on('click', function () {
            onDecision(false, $('#kyc-modal-reason').val());
        });

        new bootstrap.Modal(document.getElementById('kyc-modal')).show();
    };

    Admin.openAccountModal = function (user, onSaved) {
        $('#account-modal-name').text(user.name);
        $('#account-modal-status').text(user.isActive ? 'Active' : 'Deactivated')
            .attr('class', 'badge ' + (user.isActive ? 'text-bg-success' : 'text-bg-danger'));
        $('#account-modal-phone').text(user.phone);
        $('#account-modal-email').text(user.email || '—');
        $('#account-modal-role').text(user.roleLabel);

        if (!user.isActive && user.deactivationReason) {
            $('#account-modal-deactivation').prop('hidden', false)
                .html('<strong>Deactivated:</strong> ' + Admin.escapeHtml(user.deactivationReason));
        } else {
            $('#account-modal-deactivation').prop('hidden', true);
        }

        $('#account-new-password').val('');
        $('#account-password-reason').val('');

        $('#account-active-heading').text(user.isActive ? 'Deactivate account' : 'Reactivate account');
        $('#account-active-hint').text(user.isActive
            ? 'They will be signed out everywhere and cannot sign in again. Their tasks, bids and history stay intact.'
            : 'They will be able to sign in again.');
        $('#account-active-reason-wrap').prop('hidden', !user.isActive);
        $('#account-active-reason').prop('required', user.isActive).val('');
        $('#account-active-submit')
            .text(user.isActive ? 'Deactivate' : 'Reactivate')
            .attr('class', 'btn btn-sm ' + (user.isActive ? 'btn-outline-danger' : 'btn-success'));

        $('#account-password-form').off('submit').on('submit', function (event) {
            event.preventDefault();

            App.api.post('/api/users/' + user.id + '/reset-password', Admin.formToJson(this)).then(function (result) {
                bootstrap.Modal.getInstance(document.getElementById('account-modal')).hide();
                showToast('Success', result.wasGenerated
                    ? 'Password reset for ' + result.user.name + '. Generated password: '
                      + result.generatedPassword + ' — copy it now, it will not be shown again.'
                    : 'Password reset for ' + result.user.name + ' to the one you typed.', 'success');
            });
        });

        $('#account-active-form').off('submit').on('submit', function (event) {
            event.preventDefault();

            var makeActive = !user.isActive;
            var data = Admin.formToJson(this);
            data.IsActive = makeActive;

            App.confirmAction({ title: (makeActive ? 'Reactivate ' : 'Deactivate ') + user.name + '?' }).then(function (ok) {
                if (!ok) return;

                App.api.post('/api/users/' + user.id + '/active', data).then(function (result) {
                    bootstrap.Modal.getInstance(document.getElementById('account-modal')).hide();
                    showToast('Success', makeActive
                        ? result.user.name + ' can sign in again.'
                        : result.user.name + ' has been deactivated and signed out everywhere.', 'success');
                    if (onSaved) onSaved();
                });
            });
        });

        new bootstrap.Modal(document.getElementById('account-modal')).show();
    };

    Admin.openAddressModal = function (address, onSaved) {
        $('#address-modal-title').text(address ? 'Edit ' + address.label + ' address' : 'Add an address');
        $('#address-id').val(address ? address.id : '');
        $('#address-label').val(address ? address.label : 'Home');
        $('#address-house-number').val(address ? address.houseNumber || '' : '');
        $('#address-pincode').val(address ? address.pincode || '' : '');
        $('#address-line1').val(address ? address.line1 || '' : '');
        $('#address-line2').val(address ? address.line2 || '' : '');
        $('#address-landmark').val(address ? address.landmark || '' : '');
        $('#address-city').val(address ? address.city || '' : '');
        $('#address-state').val(address ? address.state || '' : '');
        $('#address-latitude').val(address && address.latitude != null ? address.latitude : '');
        $('#address-longitude').val(address && address.longitude != null ? address.longitude : '');
        $('#address-is-default').prop('checked', !!(address && address.isDefault));

        $('#address-pin-status').removeClass('text-danger text-success').text(
            address && address.hasCoordinates
                ? 'Pinned at ' + Number(address.latitude).toFixed(5) + ', ' + Number(address.longitude).toFixed(5)
                : 'No pin saved. Without one this address cannot be matched by distance.');

        $('#address-form').off('submit').on('submit', function (event) {
            event.preventDefault();

            var id = $('#address-id').val();
            var data = Admin.formToJson(this);
            var lat = $('#address-latitude').val();
            var lon = $('#address-longitude').val();
            data.Latitude = lat === '' ? null : lat;
            data.Longitude = lon === '' ? null : lon;

            var request = id ? App.api.put('/api/addresses/' + id, data) : App.api.post('/api/addresses', data);

            request.then(function (saved) {
                bootstrap.Modal.getInstance(document.getElementById('address-modal')).hide();
                showToast('Success', id ? 'Address updated.' : "'" + saved.label + "' address saved.", 'success');
                if (onSaved) onSaved(saved);
            });
        });

        new bootstrap.Modal(document.getElementById('address-modal')).show();
    };

    window.Admin = Admin;

    // The access-token cookie is short-lived on purpose; this is what keeps a
    // portal session alive past that without ever showing a login screen,
    // as long as the refresh token (30 days of activity) is still good. A
    // failure here is silent — the user only notices at the point the access
    // token actually expires and the normal login redirect takes over.
    function wireSessionRefresh() {
        var portal = (location.pathname.match(/^\/(customer|provider|admin)(\/|$)/) || [])[1];
        if (!portal) return;

        setInterval(function () {
            App.api.post('/' + portal + '/refresh-session', {}, { silent: true }).catch(function () {});
        }, 20 * 60 * 1000);
    }

    // Pub-sub for "something changed, you may want to refetch." A page that
    // already has its own load() just registers it once — App.realtime.on('tasks', load)
    // — and gets called whenever the server says that topic changed, instead
    // of polling on an interval or only finding out on the next page load.
    var realtimeHandlers = {};

    App.realtime = {
        on: function (topic, handler) {
            (realtimeHandlers[topic] = realtimeHandlers[topic] || []).push(handler);
        },
    };

    function dispatchRealtime(topic) {
        (realtimeHandlers[topic] || []).forEach(function (handler) {
            try { handler(); } catch (e) { /* one page's handler failing must not break another's */ }
        });
    }

    function notificationRow(note) {
        return '<a class="notification-row' + (note.isRead ? '' : ' is-unread') + '" href="'
            + (note.link || '#') + '">'
            + '<span class="notification-icon">' + note.icon + '</span>'
            + '<span class="notification-text">'
            + '<span class="notification-title">' + Admin.escapeHtml(note.title) + '</span>'
            + '<span class="notification-body">' + Admin.escapeHtml(note.body) + '</span>'
            + '<span class="notification-age">' + Admin.escapeHtml(note.age) + '</span>'
            + '</span></a>';
    }

    function refreshNotificationBell() {
        var $menu = $('[data-notification-menu]');
        if ($menu.length === 0) return;

        App.api.get('/api/notifications/summary', { silent: true }).then(function (summary) {
            $menu.find('[data-notification-badge]').prop('hidden', summary.unreadCount === 0);
            $menu.find('[data-notification-count]').text(summary.unreadCount);

            var $list = $menu.find('[data-notification-list]');
            $list.html(summary.recent.length === 0
                ? '<div class="px-3 py-4 text-center text-secondary small" data-notification-empty>'
                  + 'Nothing yet. We will tell you when a job moves.</div>'
                : summary.recent.map(notificationRow).join(''));
        }).catch(function () {});
    }

    // One hub for the whole app; connects only on a portal page (the public
    // storefront has nothing that needs to arrive live) and only once
    // authenticated — the hub itself requires that, so an anonymous visitor's
    // connection simply never completes, harmlessly.
    function wireRealtime() {
        var portal = (location.pathname.match(/^\/(customer|provider|admin)(\/|$)/) || [])[1];
        if (!portal || typeof signalR === 'undefined') return;

        var connection = new signalR.HubConnectionBuilder()
            .withUrl('/hubs/app')
            .withAutomaticReconnect()
            .build();

        connection.on('refresh', function (topic) { dispatchRealtime(topic); });

        connection.on('notification', function (note) {
            showToast(note.title, note.body, 'info');
            refreshNotificationBell();
        });

        connection.start().catch(function () { /* silent — the bell still works on the next page load */ });
    }

    function wireLoginForm() {
        $(document).on('submit', '[data-login-form]', function (event) {
            event.preventDefault();
            var form = this;
            $('#login-form-errors').empty();

            App.api.post(window.location.pathname, Admin.formToJson(form), { silent: true })
                .then(function (body) { window.location.href = body.redirectTo; })
                .catch(function (error) { Admin.showFormErrors('#login-form-errors', error.message); });
        });
    }

    function wireOtpAuthForm() {
        $('[data-otp-auth]').each(function () {
            var $card = $(this);
            var $phoneStep = $card.find('[data-otp-step="phone"]');
            var $codeStep = $card.find('[data-otp-step="code"]');
            var $passwordStep = $card.find('[data-otp-step="password"]');
            var $requestForm = $card.find('[data-otp-request-form]');
            var $verifyForm = $card.find('[data-otp-verify-form]');

            function requestCode(phone) {
                $('#otp-form-errors').empty();

                return App.api.post($requestForm.attr('action'), {
                    Phone: phone,
                    ReturnUrl: $requestForm.find('[name="ReturnUrl"]').val(),
                }, { silent: true }).then(function (body) {
                    $card.find('[data-otp-phone-label]').text(phone);
                    $card.find('[data-otp-phone-field]').val(phone);

                    var $dev = $card.find('[data-otp-dev-code]');
                    if (body.devCode) {
                        $dev.text('No SMS provider is set up yet — for now, the code is ' + body.devCode + '.')
                            .prop('hidden', false);
                    } else {
                        $dev.prop('hidden', true);
                    }

                    $phoneStep.hide();
                    $codeStep.prop('hidden', false).show();
                    $card.find('#otp-code').val('').trigger('focus');
                }).catch(function (error) {
                    Admin.showFormErrors('#otp-form-errors', error.message);
                });
            }

            $requestForm.on('submit', function (event) {
                event.preventDefault();
                requestCode($(this).find('[name="Phone"]').val());
            });

            $verifyForm.on('submit', function (event) {
                event.preventDefault();
                $('#otp-form-errors').empty();

                var data = Admin.formToJson(this);
                data.Name = data.Name || null;

                App.api.post($verifyForm.attr('action'), data, { silent: true })
                    .then(function (body) { window.location.href = body.redirectTo; })
                    .catch(function (error) { Admin.showFormErrors('#otp-form-errors', error.message); });
            });

            $card.on('click', '[data-otp-resend]', function () {
                requestCode($card.find('[data-otp-phone-field]').val());
            });

            $card.on('click', '[data-otp-change-number]', function () {
                $('#otp-form-errors').empty();
                $codeStep.hide();
                $phoneStep.show();
            });

            $card.on('click', '[data-otp-use-password]', function () {
                var phone = $card.find('[data-otp-phone-field]').val();
                $passwordStep.find('[data-otp-password-identifier]').val(phone);
                $passwordStep.find('[data-otp-password-phone-label]').text(phone);
                $('#login-form-errors').empty();
                $codeStep.hide();
                $passwordStep.prop('hidden', false).show();
                $passwordStep.find('#otp-password').trigger('focus');
            });

            $card.on('click', '[data-otp-password-back]', function () {
                $('#login-form-errors').empty();
                $passwordStep.hide();
                $codeStep.prop('hidden', false).show();
            });
        });
    }

    var CART_MAX_QUANTITY = 20;

    function formatMoney(amount) {
        return Math.round(amount).toLocaleString('en-IN');
    }

    function renderQtyStepper(serviceItemId, quantity) {
        return '<div class="qty-stepper" data-qty-stepper data-service-id="' + serviceItemId + '">'
            + '<button type="button" data-qty-decrement aria-label="Reduce quantity">&minus;</button>'
            + '<span class="qty-value" data-qty-value>' + quantity + '</span>'
            + '<button type="button" data-qty-increment aria-label="Increase quantity"'
            + (quantity >= CART_MAX_QUANTITY ? ' disabled' : '') + '>+</button>'
            + '</div>';
    }

    function applyCartToPage(cart) {
        var byId = {};
        cart.lines.forEach(function (line) { byId[line.serviceItemId] = line; });

        $('[data-cart-control]').each(function () {
            var $control = $(this);
            var id = $control.closest('[data-service-card]').data('service-id');
            var line = byId[id];

            $control.html(line
                ? renderQtyStepper(id, line.quantity)
                : '<button type="button" class="btn btn-sm btn-uc-outline" data-add-to-cart>Add</button>');
        });

        $('[data-cart-line]').each(function () {
            var $row = $(this);
            var id = $row.data('service-id');
            var line = byId[id];

            if (!line) { $row.remove(); return; }

            $row.find('[data-qty-stepper]').replaceWith(renderQtyStepper(id, line.quantity));
            $row.find('[data-line-total]').text('₹' + formatMoney(line.lineTotal));
        });

        $('[data-cart-summary]').each(function () {
            var html = cart.lines.map(function (line) {
                return '<dt class="col-8 fw-normal text-secondary">' + Admin.escapeHtml(line.serviceItemName)
                    + (line.quantity > 1 ? ' x ' + line.quantity : '') + '</dt>'
                    + '<dd class="col-4 text-end">₹' + formatMoney(line.lineTotal) + '</dd>';
            }).join('');
            $(this).html(html);
        });

        $('[data-cart-summary-total]').text('₹' + formatMoney(cart.total));

        $('[data-cart-badge]').each(function () {
            $(this).prop('hidden', cart.itemCount === 0)
                .find('[data-cart-badge-count]').text(cart.itemCount);
        });

        $('[data-cart-bar]').each(function () {
            $(this).prop('hidden', cart.isEmpty);
            $(this).find('[data-cart-bar-total]').text('₹' + formatMoney(cart.total));
            $(this).find('[data-cart-bar-count]').text(
                cart.itemCount + ' item' + (cart.itemCount === 1 ? '' : 's') + ' in your cart');
        });

        $(document).trigger('cart:updated', [cart]);
    }

    function wireCartControls() {
        $(document).on('click', '[data-add-to-cart]', function () {
            var id = $(this).closest('[data-service-card]').data('service-id');
            App.api.post('/api/cart/items', { serviceItemId: id, quantity: 1 }).then(applyCartToPage);
        });

        $(document).on('click', '[data-qty-increment]', function () {
            var $stepper = $(this).closest('[data-qty-stepper]');
            var id = $stepper.data('service-id');
            var quantity = parseInt($stepper.find('[data-qty-value]').text(), 10) + 1;
            App.api.put('/api/cart/items/' + id, { quantity: quantity }).then(applyCartToPage);
        });

        $(document).on('click', '[data-qty-decrement]', function () {
            var $stepper = $(this).closest('[data-qty-stepper]');
            var id = $stepper.data('service-id');
            var quantity = parseInt($stepper.find('[data-qty-value]').text(), 10) - 1;
            App.api.put('/api/cart/items/' + id, { quantity: quantity }).then(applyCartToPage);
        });

        $(document).on('click', '[data-cart-remove]', function () {
            var id = $(this).closest('[data-cart-line]').data('service-id');
            App.api.del('/api/cart/items/' + id).then(applyCartToPage);
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
        wireLoginForm();
        wireOtpAuthForm();
        wireCartControls();
        wireSessionRefresh();
        wireRealtime();
    });

})(window, jQuery);
