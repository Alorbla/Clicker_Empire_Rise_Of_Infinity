mergeInto(LibraryManager.library, {
  IdleHra_SaveExport: function(fileNamePtr, base64ContentPtr) {
    try {
      var fileName = UTF8ToString(fileNamePtr);
      var base64 = UTF8ToString(base64ContentPtr);
      var binary = atob(base64);
      var bytes = new Uint8Array(binary.length);
      for (var i = 0; i < binary.length; i++) {
        bytes[i] = binary.charCodeAt(i);
      }

      var blob = new Blob([bytes], { type: 'application/json' });
      var url = URL.createObjectURL(blob);
      var a = document.createElement('a');
      a.href = url;
      a.download = fileName || 'save_export.json';
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      URL.revokeObjectURL(url);
    } catch (e) {
      console.error('IdleHra_SaveExport failed', e);
    }
  },

  IdleHra_OpenImportFilePicker: function(gameObjectNamePtr, callbackMethodPtr) {
    try {
      var gameObjectName = UTF8ToString(gameObjectNamePtr);
      var callbackMethod = UTF8ToString(callbackMethodPtr);

      var input = document.createElement('input');
      input.type = 'file';
      input.accept = '.json,application/json,text/json';
      input.style.display = 'none';

      input.onchange = function(evt) {
        var file = evt && evt.target && evt.target.files ? evt.target.files[0] : null;
        if (!file) {
          return;
        }

        var reader = new FileReader();
        reader.onload = function(loadEvt) {
          try {
            var text = loadEvt && loadEvt.target ? (loadEvt.target.result || '') : '';
            var utf8Base64 = btoa(unescape(encodeURIComponent(text)));
            if (typeof SendMessage === 'function') {
              SendMessage(gameObjectName, callbackMethod, utf8Base64);
            } else {
              console.error('SendMessage is not available for WebGL import callback.');
            }
          } catch (innerError) {
            console.error('IdleHra_OpenImportFilePicker read callback failed', innerError);
          }
        };

        reader.onerror = function(err) {
          console.error('IdleHra_OpenImportFilePicker reader failed', err);
        };

        reader.readAsText(file);
      };

      document.body.appendChild(input);
      input.click();
      document.body.removeChild(input);
    } catch (e) {
      console.error('IdleHra_OpenImportFilePicker failed', e);
    }
  }
});
