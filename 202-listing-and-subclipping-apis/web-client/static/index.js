(function () {

    // settings
    var baseUrl = '/';
    var assetsUrl = baseUrl + 'api/assets';
    var baseAssetUrl = baseUrl + 'api/assets/';
    var subclipsUrl = baseUrl + 'api/subclips';
    var baseJobsUrl = baseUrl + 'api/subclips/jobs/';
    var tableRefreshInterval = 10000; // 10 seconds
    var jobRefreshInterval = 5000; // 5 seconds

    // initilization
    $(function () {
        var keyboadShortcutConfig = new AMVE.AdobePremierProShortcutConfig();
        var playerOptions = {
            nativeControlsForTouch: false,
            autoplay: true,
            controls: true,
            poster: '',
            plugins: {
                AMVE: {
                    containerId: 'amve',
                    clipdataCallback: onClipdataCallback,
                    keyboardShortcutConfig: keyboadShortcutConfig
                }
            }
        };

        // load settings
        $.getJSON('/config.json', function (data) {

            baseUrl = data.baseUrl;
            assetsUrl = baseUrl + 'api/assets';
            baseAssetUrl = baseUrl + 'api/assets/';
            subclipsUrl = baseUrl + 'api/subclips';
            baseJobsUrl = baseUrl + 'api/subclips/jobs/';

            // initialize Azure Media Player + Azure Media Video Editor
            amp('ampEditor', playerOptions);

            var table = $('#assets').DataTable({
                serverSide: true,
                bSort: false,
                searching: true,
                ajax: function (data, callback, settings) {
                    $.getJSON(assetsUrl + '?skip=' + data.start + '&take=' + data.length + '&search=' + data.search.value,
                        function (data) {
                            // renaming Data as data
                            data.data = data.Data;
                            delete data.Data;
                            data.recordsFiltered = data.recordsTotal = data.Total;
                            callback(data);
                        }
                    );
                },
                rowCallback: rowCallback,
                iDisplayLength: 10,
                bLengthChange: false,
                columns: [{
                        data: 'Name'
                    },
                    {
                        data: 'Type'
                    },
                    {
                        data: 'Files',
                        render: function (d) {
                            return (d || []).length;
                        }
                    },
                    {
                        data: 'Files',
                        render: totalSize
                    },
                    {
                        data: null,
                        className: 'right aligned'
                    }
                ]
            });

            // self update
            setInterval(function () {
                table.ajax.reload(null, false);
            }, tableRefreshInterval);

            function totalSize(data) {
                return formatBytes(
                    (data || []).reduce(function (prev, actual) {
                        return prev + actual.Size;
                    }, 0)
                );
            }

            function rowCallback(row, data, index) {
                var buttons = '<div class="ui icon basic buttons">' +
                    '<a alt="Explore asset files" title="Explore asset files" class="ui button item" data-download-asset="' + data.Id + '"><i class="file video outline icon"></i></a>';
                if (data.BaseStreamingUrl) {
                    buttons += '<a alt="Open video subclipper player" title="Open video subclipper player" class="ui button item" data-edit-asset="' + data.Id + '"><i class="video play icon"></i></a>';
                }
                buttons += '</div></td></tr>';

                $('td:eq(4)', row).html(buttons);
            }
        });
    });

    // click event handlers

    $(document).on('click', 'a[data-download-asset]', function () {
        var that = this,
            assetId = $(this).data('download-asset'),
            oldState = $(this).html(),
            url = baseAssetUrl + encodeURIComponent(assetId.substring('nb:cid:UUID:'.length));

        $(this).html('<div class="ui active inline mini loader"></div>');

        $.getJSON(url, function (asset) {
            $(that).html(oldState);

            $('.ui.modal .header').text(asset.Name);

            var data = asset.Files.sort(function (a, b) {
                return a.Name.toLowerCase().localeCompare(b.Name.toLowerCase());
            });

            if ($.fn.DataTable.isDataTable('#files')) {
                var table = $('#files').dataTable();
                table.fnClearTable();
                table.fnAddData(data);
                table.fnDraw();
            } else {
                $('#files').DataTable({
                    data: data,
                    iDisplayLength: 10,
                    bLengthChange: false,
                    columns: [{
                            data: 'Name',
                            fnCreatedCell: function (nTd, sData, oData, iRow, iCol) {
                                if (oData.DownloadUrl) {
                                    $(nTd).html('<a alt="Download file" title="Download file" target="_blank" href="'+oData.DownloadUrl+'">'+oData.Name+'&nbsp;<i class="cloud download small icon"></i></a>');
                                }
                            }
                        },{
                            data: 'Size'
                        }
                    ]
                });

                $('#files').removeAttr('style');
            }

            $('.ui.modal').modal('show');
        });
    });

    $(document).on('click', 'a[data-edit-asset]', function () {
        var that = this,
            assetId = $(this).data('edit-asset'),
            oldState = $(this).html(),
            url = baseAssetUrl + encodeURIComponent(assetId.substring('nb:cid:UUID:'.length));

        $(this).html('<div class="ui active inline mini loader"></div>');

        $.getJSON(url, function (asset) {

            $(that).html(oldState);
            $('#editor').removeAttr('style');

            var player = amp('ampEditor');
            player.editingAssetId = assetId;
            player.ready(function () {
                player.src([{
                    src: asset.BaseStreamingUrl,
                    type: "application/vnd.ms-sstr+xml"
                }]);

                // Make sure that rendered mode is selected
                $(".amve-btn.amve-btn-text.amve-rendered-btn").click();
            });
        });
    });

    $(document).keyup(function (e) {
        // esc handler
        if (e.keyCode === 27) $('#btnCloseEditor').click();
    });

    $(document).on('click', '#btnCloseEditor', function () {
        amp('ampEditor').pause();
        $('#editor').attr('style', 'opacity: 0; pointer-events: none;');
    });

    // editor callback functions

    function onClipdataCallback(clipData) {

        $('#btnCloseEditor').click();

        if (clipData) {
            var data = {
                SourceAssetId: amp('ampEditor').editingAssetId,
                Name: clipData.title || 'No title',
                Start: clipData.markIn,
                End: clipData.markOut
            };

            $.ajax({
                type: 'POST',
                url: subclipsUrl,
                contentType: 'application/json',
                dataType: 'json',
                data: JSON.stringify(data),
                success: function (response) {
                    if (response.JobId) {
                        response.Name = clipData.title;
                        addJobFromReponse(response);
                    }
                },
                error: function (xhr, error) {
                    console.log('error', error);
                }
            });
        } else {
            alert('Something went wrong while processing the subclip metadata, try again later.');
        }
    }

    function addJobFromReponse(response) {
        $('#notifications').append(createNotification(
            response.JobId,
            'Subclipping Job: ' + response.Name,
            'Starting'
        ));

        var intervalId = setInterval(function () {
            url = baseJobsUrl + encodeURIComponent(response.JobId.substring('nb:jid:UUID:'.length));
            $.getJSON(url, function (data) {
                updateNotification(response.JobId, data.State);
            });
        }, jobRefreshInterval);
    }

    function createNotification(id, title, state) {
        id = id.replace(/[:-]/g, '');
        var notification = '<div id="' + id + '" class="ui icon message">' +
            '<i class="notched circle loading icon"></i>' +
            '<div class="content">' +
            '<div class="header">' + title + '</div>' +
            '<p>' + state + '</p>' +
            '</div></div>';
        return notification;
    }

    function updateNotification(id, state) {
        var states = ['Finished', 'Canceled', 'Error'];
        id = id.replace(/[:-]/g, '');
        if (states.indexOf(state) === -1) {
            $('#' + id).find('p').text(state);
        } else {
            $('#' + id).remove();
        }
    }

    // formating helper functions

    function formatBytes(bytes, decimals) {
        if (bytes === 0) return '0 Bytes';
        var k = 1000,
            dm = decimals || 2,
            sizes = ['Bytes', 'KB', 'MB', 'GB', 'TB', 'PB', 'EB', 'ZB', 'YB'],
            i = Math.floor(Math.log(bytes) / Math.log(k));
        return parseFloat((bytes / Math.pow(k, i)).toFixed(dm)) + ' ' + sizes[i];
    }

})();