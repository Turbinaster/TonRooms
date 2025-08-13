//$(':checkbox').map(function () { return this.id; }).get().join();

var quill;
$(document).ready(function () {
    $('#search').keyup(function () {
        var v = $(this).val().toLowerCase();
        if (v == '') $('tbody tr').show();
        else {
            $('tbody tr').hide();
            $('tbody tr').each(function () {
                var tr = $(this);
                tr.find('td').each(function () {
                    var text = $(this).text().toLowerCase();
                    if (text.indexOf(v) > -1) tr.show();
                });
            });
        }
    });
    $('.tabs').lightTabs();
    $('select:not(.multiselect)').niceSelect();
    $('.multiselect').SumoSelect();

    $(window).keyup(function (e) {
        if (e.keyCode == 13) {
            $('.default').click();
        }
    });

    if ($('#editor').length) {
        quill = new Quill('#editor', {
            modules: {
                toolbar: toolbarOptions
            },
            theme: 'snow'
        });
    }
});

function post(url, params, s) {
    $.ajax({
        url: url,
        type: 'POST',
        traditional: true,
        data: params,
        success: function (data) {
            enable();
            if (data.r == 'error') er(data.m);
            else if (s !== undefined) s(data);
        },
        error: function (request, status, error) {
            enable();
            notify(request.responseText, 'error');
        }
    });
}

function dialog(el, w) {
    if (w == undefined) w = 500;
    $('.main').css('filter', 'blur(1px)');
    var clone = $(el).clone();
    swal({
        content: $(el)[0],
        buttons: false
    }).then(function (value) {
        $('.main').css('filter', 'none');
        $('.modals').append(clone);
    });
    setTimeout(function () { $('.swal-modal').width(w).find('input:first, textarea:first').focus().select(); }, 100);
}
function closeDialog() {
    swal.close();
}

function notify(text, bg) {
    $.amaran({
        content: {
            bgcolor: bg,
            color: '#fff',
            message: text
        },
        theme: 'colorful',
        position: 'top right',
        cssanimationIn: 'bounceInRight',
        cssanimationOut: 'zoomOutRight',
        delay: 3000
    });
}
function info(text) {
    notify(text, '#15ab12');
}
function er(text) {
    notify(text, '#ad1010');
}

function inputs(el) {
    var o = {};
    $(el).find(':input').each(function () {
        var val = $(this).val();
        if ($(this).attr('type') == 'radio') {
            var name = $(this).attr('name');
            val = $('[name="' + name + '"]:checked').val();
            o[name] = val;
        }
        else if (this.id != '') {
            if ($(this).attr('type') == 'checkbox') val = $(this).prop('checked');
            else if (Array.isArray(val)) val = val.join();
            o[this.id] = val;
        }
    });
    return o;
}

function serialize(p) {
    var str = [];
    for (var p in p)
        if (p.hasOwnProperty(p)) {
            str.push(encodeURIComponent(p) + "=" + encodeURIComponent(p[p]));
        }
    return str.join("&");
}

function disable(el) {
    $(el).attr('tag', $(el).html());
    var w = $(el).width() + 1;
    $(el).width(w).html('<i class="fas fa-spinner fa-spin"></i>').addClass('disabled').attr('disabled', 'disabled');
}

function enable() {
    $('.disabled').each(function () {
        var tag = $(this).attr('tag');
        if (tag != undefined && tag != '') {
            $(this).html(tag).removeClass('disabled').removeAttr('disabled');
        }
    });
}

function autoGrow(element) {
    element.style.height = "5px";
    element.style.height = (element.scrollHeight - 8) + "px";
}

String.prototype.format = function () {
    var args = arguments;
    return this.replace(/\{(\d+)\}/g, function (m, n) {
        return args[n];
    });
};

(function ($) {

    $.fn.lightTabs = function (options) {

        return this.each(function () {
            var tabs = this;
            i = 0;

            var showPage = function (i) {
                $(tabs).children('div').hide();
                $(tabs).children('div').eq(i).show();
                $(tabs).children('ul').children('li').removeClass('active');
                $(tabs).children('ul').children('li').eq(i).addClass('active');
            }

            showPage(0);

            $(tabs).children('ul').children('li').each(function (index, element) {
                $(element).attr('data-page', i);
                i++;
            });

            $(tabs).children('ul').children('li').click(function () {
                showPage(parseInt($(this).attr('data-page')));
            });
        });
    };
})(jQuery);
function openTab(tab, selector) {
    $(selector + ' ul li[data-page="' + tab + '"]').click();
}

var toolbarOptions = [
    ['bold', 'italic', 'underline', 'strike'],        // toggled buttons
    ['blockquote', 'code-block'],

    [{ 'list': 'ordered' }, { 'list': 'bullet' }],
    [{ 'indent': '-1' }, { 'indent': '+1' }],          // outdent/indent
    [{ 'direction': 'rtl' }],                         // text direction

    [{ 'size': ['small', false, 'large', 'huge'] }],  // custom dropdown
    [{ 'header': [1, 2, 3, 4, 5, 6, false] }],
    ['link', 'image', 'video', 'formula'],          // add's image support
    [{ 'color': [] }, { 'background': [] }],          // dropdown with defaults from theme
    [{ 'font': [] }],
    [{ 'align': [] }],

    ['clean']                                         // remove formatting button
];

