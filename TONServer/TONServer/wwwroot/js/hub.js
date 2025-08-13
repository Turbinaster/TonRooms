var host = location.protocol + '//' + location.hostname + (location.port ? ':' + location.port : '') + '/bot';
const hub = new signalR.HubConnectionBuilder().withUrl(host).build();

hub.on('send', function (data) {
    $('#signalr').append('<p>' + data + '</p>');
});
hub.on('session', function (session, data) {
    if ($('#session').val() == session) console.log(data);
});
hub.on('avatar', function (session, data) {
    if ($('#session').val() == session) $('.avatar').attr('src', data);
});
hub.on('profile_edit_avatar', function (session, data) {
    if ($('#session').val() == session) {
        $('#profile_edit_avatar_img').attr('src', data);
        $('#profile_edit_avatar').val(data);
    }
});
hub.on('showNotify', function (address) {
    if ($('#address').val() == address) $('.notify').show();
});
hub.on('hideNotify', function (address) {
    if ($('#address').val() == address) $('.notify').hide();
});

hub.start();