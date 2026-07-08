var __create = Object.create;
var __defProp = Object.defineProperty;
var __getOwnPropDesc = Object.getOwnPropertyDescriptor;
var __getOwnPropNames = Object.getOwnPropertyNames;
var __getProtoOf = Object.getPrototypeOf;
var __hasOwnProp = Object.prototype.hasOwnProperty;
var __commonJS = (cb, mod) => function __require() {
  try {
    return mod || (0, cb[__getOwnPropNames(cb)[0]])((mod = { exports: {} }).exports, mod), mod.exports;
  } catch (e) {
    throw mod = 0, e;
  }
};
var __export = (target, all) => {
  for (var name in all)
    __defProp(target, name, { get: all[name], enumerable: true });
};
var __copyProps = (to, from, except, desc) => {
  if (from && typeof from === "object" || typeof from === "function") {
    for (let key of __getOwnPropNames(from))
      if (!__hasOwnProp.call(to, key) && key !== except)
        __defProp(to, key, { get: () => from[key], enumerable: !(desc = __getOwnPropDesc(from, key)) || desc.enumerable });
  }
  return to;
};
var __toESM = (mod, isNodeMode, target) => (target = mod != null ? __create(__getProtoOf(mod)) : {}, __copyProps(
  // If the importer is in node compatibility mode or this is not an ESM
  // file that has been converted to a CommonJS file using a Babel-
  // compatible transform (i.e. "__esModule" has not been set), then set
  // "default" to the CommonJS "module.exports" for node compatibility.
  isNodeMode || !mod || !mod.__esModule ? __defProp(target, "default", { value: mod, enumerable: true }) : target,
  mod
));
var __toCommonJS = (mod) => __copyProps(__defProp({}, "__esModule", { value: true }), mod);

// node_modules/papaparse/papaparse.js
var require_papaparse = __commonJS({
  "node_modules/papaparse/papaparse.js"(exports2, module2) {
    (function(root, factory) {
      if (typeof define === "function" && define.amd) {
        define([], factory);
      } else if (typeof module2 === "object" && typeof exports2 !== "undefined") {
        module2.exports = factory();
      } else {
        root.Papa = factory();
      }
    })(exports2, function moduleFactory() {
      "use strict";
      var global = (function() {
        if (typeof self !== "undefined") {
          return self;
        }
        if (typeof window !== "undefined") {
          return window;
        }
        if (typeof global !== "undefined") {
          return global;
        }
        return {};
      })();
      function getWorkerBlob() {
        var URL = global.URL || global.webkitURL || null;
        var code = moduleFactory.toString();
        return Papa2.BLOB_URL || (Papa2.BLOB_URL = URL.createObjectURL(new Blob(["var global = (function() { if (typeof self !== 'undefined') { return self; } if (typeof window !== 'undefined') { return window; } if (typeof global !== 'undefined') { return global; } return {}; })(); global.IS_PAPA_WORKER=true; ", "(", code, ")();"], { type: "text/javascript" })));
      }
      var IS_WORKER = !global.document && !!global.postMessage, IS_PAPA_WORKER = global.IS_PAPA_WORKER || false;
      var workers = {}, workerIdCounter = 0;
      var Papa2 = {};
      Papa2.parse = CsvToJson;
      Papa2.unparse = JsonToCsv;
      Papa2.RECORD_SEP = String.fromCharCode(30);
      Papa2.UNIT_SEP = String.fromCharCode(31);
      Papa2.BYTE_ORDER_MARK = "\uFEFF";
      Papa2.BAD_DELIMITERS = ["\r", "\n", '"', Papa2.BYTE_ORDER_MARK];
      Papa2.WORKERS_SUPPORTED = !IS_WORKER && !!global.Worker;
      Papa2.NODE_STREAM_INPUT = 1;
      Papa2.LocalChunkSize = 1024 * 1024 * 10;
      Papa2.RemoteChunkSize = 1024 * 1024 * 5;
      Papa2.DefaultDelimiter = ",";
      Papa2.Parser = Parser;
      Papa2.ParserHandle = ParserHandle;
      Papa2.NetworkStreamer = NetworkStreamer;
      Papa2.FileStreamer = FileStreamer;
      Papa2.StringStreamer = StringStreamer;
      Papa2.ReadableStreamStreamer = ReadableStreamStreamer;
      if (typeof PAPA_BROWSER_CONTEXT === "undefined") {
        Papa2.DuplexStreamStreamer = DuplexStreamStreamer;
      }
      if (global.jQuery) {
        var $ = global.jQuery;
        $.fn.parse = function(options) {
          var config = options.config || {};
          var queue = [];
          this.each(function(idx) {
            var supported = $(this).prop("tagName").toUpperCase() === "INPUT" && $(this).attr("type").toLowerCase() === "file" && global.FileReader;
            if (!supported || !this.files || this.files.length === 0)
              return true;
            for (var i = 0; i < this.files.length; i++) {
              queue.push({
                file: this.files[i],
                inputElem: this,
                instanceConfig: $.extend({}, config)
              });
            }
          });
          parseNextFile();
          return this;
          function parseNextFile() {
            if (queue.length === 0) {
              if (isFunction(options.complete))
                options.complete();
              return;
            }
            var f = queue[0];
            if (isFunction(options.before)) {
              var returned = options.before(f.file, f.inputElem);
              if (typeof returned === "object") {
                if (returned.action === "abort") {
                  error("AbortError", f.file, f.inputElem, returned.reason);
                  return;
                } else if (returned.action === "skip") {
                  fileComplete();
                  return;
                } else if (typeof returned.config === "object")
                  f.instanceConfig = $.extend(f.instanceConfig, returned.config);
              } else if (returned === "skip") {
                fileComplete();
                return;
              }
            }
            var userCompleteFunc = f.instanceConfig.complete;
            f.instanceConfig.complete = function(results) {
              if (isFunction(userCompleteFunc))
                userCompleteFunc(results, f.file, f.inputElem);
              fileComplete();
            };
            Papa2.parse(f.file, f.instanceConfig);
          }
          function error(name, file, elem, reason) {
            if (isFunction(options.error))
              options.error({ name }, file, elem, reason);
          }
          function fileComplete() {
            queue.splice(0, 1);
            parseNextFile();
          }
        };
      }
      if (IS_PAPA_WORKER) {
        global.onmessage = workerThreadReceivedMessage;
      }
      function stripBom(string) {
        if (string.charCodeAt(0) === 65279) {
          return string.slice(1);
        }
        return string;
      }
      function CsvToJson(_input, _config) {
        _config = _config || {};
        var dynamicTyping = _config.dynamicTyping || false;
        if (isFunction(dynamicTyping)) {
          _config.dynamicTypingFunction = dynamicTyping;
          dynamicTyping = {};
        }
        _config.dynamicTyping = dynamicTyping;
        _config.transform = isFunction(_config.transform) ? _config.transform : false;
        if (_config.worker && Papa2.WORKERS_SUPPORTED) {
          var w = newWorker();
          w.userStep = _config.step;
          w.userChunk = _config.chunk;
          w.userComplete = _config.complete;
          w.userError = _config.error;
          _config.step = isFunction(_config.step);
          _config.chunk = isFunction(_config.chunk);
          _config.complete = isFunction(_config.complete);
          _config.error = isFunction(_config.error);
          delete _config.worker;
          w.postMessage({
            input: _input,
            config: _config,
            workerId: w.id
          });
          return;
        }
        var streamer = null;
        if (_input === Papa2.NODE_STREAM_INPUT && typeof PAPA_BROWSER_CONTEXT === "undefined") {
          streamer = new DuplexStreamStreamer(_config);
          return streamer.getStream();
        } else if (typeof _input === "string") {
          _input = stripBom(_input);
          if (_config.download)
            streamer = new NetworkStreamer(_config);
          else
            streamer = new StringStreamer(_config);
        } else if (_input.readable === true && isFunction(_input.read) && isFunction(_input.on)) {
          streamer = new ReadableStreamStreamer(_config);
        } else if (global.File && _input instanceof File || _input instanceof Object)
          streamer = new FileStreamer(_config);
        return streamer.stream(_input);
      }
      function JsonToCsv(_input, _config) {
        var _quotes = false;
        var _writeHeader = true;
        var _delimiter = ",";
        var _newline = "\r\n";
        var _quoteChar = '"';
        var _escapedQuote = _quoteChar + _quoteChar;
        var _skipEmptyLines = false;
        var _columns = null;
        var _escapeFormulae = false;
        unpackConfig();
        var quoteCharRegex = new RegExp(escapeRegExp(_quoteChar), "g");
        if (typeof _input === "string")
          _input = JSON.parse(_input);
        if (Array.isArray(_input)) {
          if (!_input.length || Array.isArray(_input[0]))
            return serialize(null, _input, _skipEmptyLines);
          else if (typeof _input[0] === "object")
            return serialize(_columns || Object.keys(_input[0]), _input, _skipEmptyLines);
        } else if (typeof _input === "object") {
          if (typeof _input.data === "string")
            _input.data = JSON.parse(_input.data);
          if (Array.isArray(_input.data)) {
            if (!_input.fields)
              _input.fields = _input.meta && _input.meta.fields || _columns;
            if (!_input.fields)
              _input.fields = Array.isArray(_input.data[0]) ? _input.fields : typeof _input.data[0] === "object" ? Object.keys(_input.data[0]) : [];
            if (!Array.isArray(_input.data[0]) && typeof _input.data[0] !== "object")
              _input.data = [_input.data];
          }
          return serialize(_input.fields || [], _input.data || [], _skipEmptyLines);
        }
        throw new Error("Unable to serialize unrecognized input");
        function unpackConfig() {
          if (typeof _config !== "object")
            return;
          if (typeof _config.delimiter === "string" && !Papa2.BAD_DELIMITERS.filter(function(value) {
            return _config.delimiter.indexOf(value) !== -1;
          }).length) {
            _delimiter = _config.delimiter;
          }
          if (typeof _config.quotes === "boolean" || typeof _config.quotes === "function" || Array.isArray(_config.quotes))
            _quotes = _config.quotes;
          if (typeof _config.skipEmptyLines === "boolean" || typeof _config.skipEmptyLines === "string")
            _skipEmptyLines = _config.skipEmptyLines;
          if (typeof _config.newline === "string")
            _newline = _config.newline;
          if (typeof _config.quoteChar === "string") {
            _quoteChar = _config.quoteChar;
            _escapedQuote = _quoteChar + _quoteChar;
          }
          if (typeof _config.header === "boolean")
            _writeHeader = _config.header;
          if (Array.isArray(_config.columns)) {
            if (_config.columns.length === 0) throw new Error("Option columns is empty");
            _columns = _config.columns;
          }
          if (_config.escapeChar !== void 0) {
            _escapedQuote = _config.escapeChar + _quoteChar;
          }
          if (_config.escapeFormulae instanceof RegExp) {
            _escapeFormulae = _config.escapeFormulae;
          } else if (typeof _config.escapeFormulae === "boolean" && _config.escapeFormulae) {
            _escapeFormulae = /^[=+\-@\t\r].*$/;
          }
        }
        function serialize(fields, data, skipEmptyLines) {
          var csv = "";
          if (typeof fields === "string")
            fields = JSON.parse(fields);
          if (typeof data === "string")
            data = JSON.parse(data);
          var hasHeader = Array.isArray(fields) && fields.length > 0;
          var dataKeyedByField = !Array.isArray(data[0]);
          if (hasHeader && _writeHeader) {
            for (var i = 0; i < fields.length; i++) {
              if (i > 0)
                csv += _delimiter;
              csv += safe(fields[i], i);
            }
            if (data.length > 0)
              csv += _newline;
          }
          for (var row = 0; row < data.length; row++) {
            var maxCol = hasHeader ? fields.length : data[row].length;
            var emptyLine = false;
            var nullLine = hasHeader ? Object.keys(data[row]).length === 0 : data[row].length === 0;
            if (skipEmptyLines && !hasHeader) {
              emptyLine = skipEmptyLines === "greedy" ? data[row].join("").trim() === "" : data[row].length === 1 && data[row][0].length === 0;
            }
            if (skipEmptyLines === "greedy" && hasHeader) {
              var line = [];
              for (var c = 0; c < maxCol; c++) {
                var cx = dataKeyedByField ? fields[c] : c;
                line.push(data[row][cx]);
              }
              emptyLine = line.join("").trim() === "";
            }
            if (!emptyLine) {
              for (var col = 0; col < maxCol; col++) {
                if (col > 0 && !nullLine)
                  csv += _delimiter;
                var colIdx = hasHeader && dataKeyedByField ? fields[col] : col;
                csv += safe(data[row][colIdx], col);
              }
              if (row < data.length - 1 && (!skipEmptyLines || maxCol > 0 && !nullLine)) {
                csv += _newline;
              }
            }
          }
          return csv;
        }
        function safe(str, col) {
          if (typeof str === "undefined" || str === null)
            return "";
          if (str.constructor === Date)
            return JSON.stringify(str).slice(1, 25);
          var needsQuotes = false;
          if (_escapeFormulae && typeof str === "string" && _escapeFormulae.test(str)) {
            str = "'" + str;
            needsQuotes = true;
          }
          var strValue = str.toString();
          var escapedQuoteStr = strValue.replace(quoteCharRegex, _escapedQuote);
          needsQuotes = needsQuotes || _quotes === true || typeof _quotes === "function" && _quotes(str, col) || Array.isArray(_quotes) && _quotes[col] || hasAny(escapedQuoteStr, Papa2.BAD_DELIMITERS) || escapedQuoteStr.indexOf(_delimiter) > -1 || strValue.indexOf(_quoteChar) > -1 || escapedQuoteStr.charAt(0) === " " || escapedQuoteStr.charAt(escapedQuoteStr.length - 1) === " ";
          return needsQuotes ? _quoteChar + escapedQuoteStr + _quoteChar : escapedQuoteStr;
        }
        function hasAny(str, substrings) {
          for (var i = 0; i < substrings.length; i++)
            if (str.indexOf(substrings[i]) > -1)
              return true;
          return false;
        }
      }
      function ChunkStreamer(config) {
        this._handle = null;
        this._finished = false;
        this._completed = false;
        this._halted = false;
        this._input = null;
        this._baseIndex = 0;
        this._partialLine = "";
        this._rowCount = 0;
        this._start = 0;
        this._nextChunk = null;
        this.isFirstChunk = true;
        this._completeResults = {
          data: [],
          errors: [],
          meta: {}
        };
        replaceConfig.call(this, config);
        this.parseChunk = function(chunk, isFakeChunk) {
          const skipFirstNLines = parseInt(this._config.skipFirstNLines) || 0;
          if (this.isFirstChunk && skipFirstNLines > 0) {
            let _newline = this._config.newline;
            if (!_newline) {
              const quoteChar = this._config.quoteChar || '"';
              _newline = this._handle.guessLineEndings(chunk, quoteChar);
            }
            const splitChunk = chunk.split(_newline);
            chunk = [...splitChunk.slice(skipFirstNLines)].join(_newline);
          }
          if (this.isFirstChunk && isFunction(this._config.beforeFirstChunk)) {
            var modifiedChunk = this._config.beforeFirstChunk(chunk);
            if (modifiedChunk !== void 0)
              chunk = modifiedChunk;
          }
          this.isFirstChunk = false;
          this._halted = false;
          var aggregate = this._partialLine + chunk;
          this._partialLine = "";
          var results = this._handle.parse(aggregate, this._baseIndex, !this._finished);
          if (this._handle.paused() || this._handle.aborted()) {
            this._halted = true;
            return;
          }
          var lastIndex = results.meta.cursor;
          if (!this._finished) {
            this._partialLine = aggregate.substring(lastIndex - this._baseIndex);
            this._baseIndex = lastIndex;
          }
          if (results && results.data)
            this._rowCount += results.data.length;
          var finishedIncludingPreview = this._finished || this._config.preview && this._rowCount >= this._config.preview;
          if (IS_PAPA_WORKER) {
            global.postMessage({
              results,
              workerId: Papa2.WORKER_ID,
              finished: finishedIncludingPreview
            });
          } else if (isFunction(this._config.chunk) && !isFakeChunk) {
            this._config.chunk(results, this._handle);
            if (this._handle.paused() || this._handle.aborted()) {
              this._halted = true;
              return;
            }
            results = void 0;
            this._completeResults = void 0;
          }
          if (!this._config.step && !this._config.chunk) {
            this._completeResults.data = this._completeResults.data.concat(results.data);
            this._completeResults.errors = this._completeResults.errors.concat(results.errors);
            this._completeResults.meta = results.meta;
          }
          if (!this._completed && finishedIncludingPreview && isFunction(this._config.complete) && (!results || !results.meta.aborted)) {
            this._config.complete(this._completeResults, this._input);
            this._completed = true;
          }
          if (!finishedIncludingPreview && (!results || !results.meta.paused))
            this._nextChunk();
          return results;
        };
        this._sendError = function(error) {
          if (isFunction(this._config.error))
            this._config.error(error);
          else if (IS_PAPA_WORKER && this._config.error) {
            global.postMessage({
              workerId: Papa2.WORKER_ID,
              error,
              finished: false
            });
          }
        };
        function replaceConfig(config2) {
          var configCopy = copy(config2);
          configCopy.chunkSize = parseInt(configCopy.chunkSize);
          if (!config2.step && !config2.chunk)
            configCopy.chunkSize = null;
          this._handle = new ParserHandle(configCopy);
          this._handle.streamer = this;
          this._config = configCopy;
        }
      }
      function NetworkStreamer(config) {
        config = config || {};
        if (!config.chunkSize)
          config.chunkSize = Papa2.RemoteChunkSize;
        ChunkStreamer.call(this, config);
        var xhr;
        if (IS_WORKER) {
          this._nextChunk = function() {
            this._readChunk();
            this._chunkLoaded();
          };
        } else {
          this._nextChunk = function() {
            this._readChunk();
          };
        }
        this.stream = function(url) {
          this._input = url;
          this._nextChunk();
        };
        this._readChunk = function() {
          if (this._finished) {
            this._chunkLoaded();
            return;
          }
          xhr = new XMLHttpRequest();
          if (this._config.withCredentials) {
            xhr.withCredentials = this._config.withCredentials;
          }
          if (!IS_WORKER) {
            xhr.onload = bindFunction(this._chunkLoaded, this);
            xhr.onerror = bindFunction(this._chunkError, this);
          }
          xhr.open(this._config.downloadRequestBody ? "POST" : "GET", this._input, !IS_WORKER);
          if (this._config.downloadRequestHeaders) {
            var headers = this._config.downloadRequestHeaders;
            for (var headerName in headers) {
              xhr.setRequestHeader(headerName, headers[headerName]);
            }
          }
          if (this._config.chunkSize) {
            var end = this._start + this._config.chunkSize - 1;
            xhr.setRequestHeader("Range", "bytes=" + this._start + "-" + end);
          }
          try {
            xhr.send(this._config.downloadRequestBody);
          } catch (err) {
            this._chunkError(err.message);
          }
          if (IS_WORKER && xhr.status === 0)
            this._chunkError();
        };
        this._chunkLoaded = function() {
          if (xhr.readyState !== 4)
            return;
          if (xhr.status < 200 || xhr.status >= 400) {
            this._chunkError();
            return;
          }
          this._start += this._config.chunkSize ? this._config.chunkSize : xhr.responseText.length;
          this._finished = !this._config.chunkSize || this._start >= getFileSize(xhr);
          this.parseChunk(xhr.responseText);
        };
        this._chunkError = function(errorMessage) {
          var errorText = xhr.statusText || errorMessage;
          this._sendError(new Error(errorText));
        };
        function getFileSize(xhr2) {
          var contentRange = xhr2.getResponseHeader("Content-Range");
          if (contentRange === null) {
            return -1;
          }
          return parseInt(contentRange.substring(contentRange.lastIndexOf("/") + 1));
        }
      }
      NetworkStreamer.prototype = Object.create(ChunkStreamer.prototype);
      NetworkStreamer.prototype.constructor = NetworkStreamer;
      function FileStreamer(config) {
        config = config || {};
        if (!config.chunkSize)
          config.chunkSize = Papa2.LocalChunkSize;
        ChunkStreamer.call(this, config);
        var reader, slice;
        var usingAsyncReader = typeof FileReader !== "undefined";
        this.stream = function(file) {
          this._input = file;
          slice = file.slice || file.webkitSlice || file.mozSlice;
          if (usingAsyncReader) {
            reader = new FileReader();
            reader.onload = bindFunction(this._chunkLoaded, this);
            reader.onerror = bindFunction(this._chunkError, this);
          } else
            reader = new FileReaderSync();
          this._nextChunk();
        };
        this._nextChunk = function() {
          if (!this._finished && (!this._config.preview || this._rowCount < this._config.preview))
            this._readChunk();
        };
        this._readChunk = function() {
          var input = this._input;
          if (this._config.chunkSize) {
            var end = Math.min(this._start + this._config.chunkSize, this._input.size);
            input = slice.call(input, this._start, end);
          }
          var txt = reader.readAsText(input, this._config.encoding);
          if (!usingAsyncReader)
            this._chunkLoaded({ target: { result: txt } });
        };
        this._chunkLoaded = function(event) {
          this._start += this._config.chunkSize;
          this._finished = !this._config.chunkSize || this._start >= this._input.size;
          this.parseChunk(event.target.result);
        };
        this._chunkError = function() {
          this._sendError(reader.error);
        };
      }
      FileStreamer.prototype = Object.create(ChunkStreamer.prototype);
      FileStreamer.prototype.constructor = FileStreamer;
      function StringStreamer(config) {
        config = config || {};
        ChunkStreamer.call(this, config);
        var remaining;
        this.stream = function(s) {
          remaining = s;
          return this._nextChunk();
        };
        this._nextChunk = function() {
          if (this._finished) return;
          var size = this._config.chunkSize;
          var chunk;
          if (size) {
            chunk = remaining.substring(0, size);
            remaining = remaining.substring(size);
          } else {
            chunk = remaining;
            remaining = "";
          }
          this._finished = !remaining;
          return this.parseChunk(chunk);
        };
      }
      StringStreamer.prototype = Object.create(StringStreamer.prototype);
      StringStreamer.prototype.constructor = StringStreamer;
      function ReadableStreamStreamer(config) {
        config = config || {};
        ChunkStreamer.call(this, config);
        var queue = [];
        var parseOnData = true;
        var streamHasEnded = false;
        this.pause = function() {
          ChunkStreamer.prototype.pause.apply(this, arguments);
          this._input.pause();
        };
        this.resume = function() {
          ChunkStreamer.prototype.resume.apply(this, arguments);
          this._input.resume();
        };
        this.stream = function(stream) {
          this._input = stream;
          this._input.on("data", this._streamData);
          this._input.on("end", this._streamEnd);
          this._input.on("error", this._streamError);
        };
        this._checkIsFinished = function() {
          if (streamHasEnded && queue.length === 1) {
            this._finished = true;
          }
        };
        this._nextChunk = function() {
          this._checkIsFinished();
          if (queue.length) {
            this.parseChunk(queue.shift());
          } else {
            parseOnData = true;
          }
        };
        this._streamData = bindFunction(function(chunk) {
          try {
            queue.push(typeof chunk === "string" ? chunk : chunk.toString(this._config.encoding));
            if (parseOnData) {
              parseOnData = false;
              this._checkIsFinished();
              this.parseChunk(queue.shift());
            }
          } catch (error) {
            this._streamError(error);
          }
        }, this);
        this._streamError = bindFunction(function(error) {
          this._streamCleanUp();
          this._sendError(error);
        }, this);
        this._streamEnd = bindFunction(function() {
          this._streamCleanUp();
          streamHasEnded = true;
          this._streamData("");
        }, this);
        this._streamCleanUp = bindFunction(function() {
          this._input.removeListener("data", this._streamData);
          this._input.removeListener("end", this._streamEnd);
          this._input.removeListener("error", this._streamError);
        }, this);
      }
      ReadableStreamStreamer.prototype = Object.create(ChunkStreamer.prototype);
      ReadableStreamStreamer.prototype.constructor = ReadableStreamStreamer;
      function DuplexStreamStreamer(_config) {
        var Duplex = require("stream").Duplex;
        var config = copy(_config);
        var parseOnWrite = true;
        var writeStreamHasFinished = false;
        var parseCallbackQueue = [];
        var stream = null;
        this._onCsvData = function(results) {
          var data = results.data;
          if (!stream.push(data) && !this._handle.paused()) {
            this._handle.pause();
          }
        };
        this._onCsvComplete = function() {
          stream.push(null);
        };
        config.step = bindFunction(this._onCsvData, this);
        config.complete = bindFunction(this._onCsvComplete, this);
        ChunkStreamer.call(this, config);
        this._nextChunk = function() {
          if (writeStreamHasFinished && parseCallbackQueue.length === 1) {
            this._finished = true;
          }
          if (parseCallbackQueue.length) {
            parseCallbackQueue.shift()();
          } else {
            parseOnWrite = true;
          }
        };
        this._addToParseQueue = function(chunk, callback) {
          parseCallbackQueue.push(bindFunction(function() {
            this.parseChunk(typeof chunk === "string" ? chunk : chunk.toString(config.encoding));
            if (isFunction(callback)) {
              return callback();
            }
          }, this));
          if (parseOnWrite) {
            parseOnWrite = false;
            this._nextChunk();
          }
        };
        this._onRead = function() {
          if (this._handle.paused()) {
            this._handle.resume();
          }
        };
        this._onWrite = function(chunk, encoding, callback) {
          this._addToParseQueue(chunk, callback);
        };
        this._onWriteComplete = function() {
          writeStreamHasFinished = true;
          this._addToParseQueue("");
        };
        this.getStream = function() {
          return stream;
        };
        stream = new Duplex({
          readableObjectMode: true,
          decodeStrings: false,
          read: bindFunction(this._onRead, this),
          write: bindFunction(this._onWrite, this)
        });
        stream.once("finish", bindFunction(this._onWriteComplete, this));
      }
      if (typeof PAPA_BROWSER_CONTEXT === "undefined") {
        DuplexStreamStreamer.prototype = Object.create(ChunkStreamer.prototype);
        DuplexStreamStreamer.prototype.constructor = DuplexStreamStreamer;
      }
      function ParserHandle(_config) {
        var MAX_FLOAT = Math.pow(2, 53);
        var MIN_FLOAT = -MAX_FLOAT;
        var FLOAT = /^\s*-?(\d+\.?|\.\d+|\d+\.\d+)([eE][-+]?\d+)?\s*$/;
        var ISO_DATE = /^((\d{4}-[01]\d-[0-3]\dT[0-2]\d:[0-5]\d:[0-5]\d\.\d+([+-][0-2]\d:[0-5]\d|Z))|(\d{4}-[01]\d-[0-3]\dT[0-2]\d:[0-5]\d:[0-5]\d([+-][0-2]\d:[0-5]\d|Z))|(\d{4}-[01]\d-[0-3]\dT[0-2]\d:[0-5]\d([+-][0-2]\d:[0-5]\d|Z)))$/;
        var self2 = this;
        var _stepCounter = 0;
        var _rowCounter = 0;
        var _input;
        var _parser;
        var _paused = false;
        var _aborted = false;
        var _delimiterError;
        var _fields = [];
        var _results = {
          // The last results returned from the parser
          data: [],
          errors: [],
          meta: {}
        };
        if (isFunction(_config.step)) {
          var userStep = _config.step;
          _config.step = function(results) {
            _results = results;
            if (needsHeaderRow())
              processResults();
            else {
              processResults();
              if (_results.data.length === 0)
                return;
              _stepCounter += results.data.length;
              if (_config.preview && _stepCounter > _config.preview)
                _parser.abort();
              else {
                _results.data = _results.data[0];
                userStep(_results, self2);
              }
            }
          };
        }
        this.parse = function(input, baseIndex, ignoreLastRow) {
          var quoteChar = _config.quoteChar || '"';
          if (!_config.newline)
            _config.newline = this.guessLineEndings(input, quoteChar);
          _delimiterError = false;
          if (!_config.delimiter) {
            var delimGuess = guessDelimiter(input, _config.newline, _config.skipEmptyLines, _config.comments, _config.delimitersToGuess);
            if (delimGuess.successful)
              _config.delimiter = delimGuess.bestDelimiter;
            else {
              _delimiterError = true;
              _config.delimiter = Papa2.DefaultDelimiter;
            }
            _results.meta.delimiter = _config.delimiter;
          } else if (isFunction(_config.delimiter)) {
            _config.delimiter = _config.delimiter(input);
            _results.meta.delimiter = _config.delimiter;
          }
          var parserConfig = copy(_config);
          if (_config.preview && _config.header)
            parserConfig.preview++;
          _input = input;
          _parser = new Parser(parserConfig);
          _results = _parser.parse(_input, baseIndex, ignoreLastRow);
          processResults();
          return _paused ? { meta: { paused: true } } : _results || { meta: { paused: false } };
        };
        this.paused = function() {
          return _paused;
        };
        this.pause = function() {
          _paused = true;
          _parser.abort();
          _input = isFunction(_config.chunk) ? "" : _input.substring(_parser.getCharIndex());
        };
        this.resume = function() {
          if (self2.streamer._halted) {
            _paused = false;
            self2.streamer.parseChunk(_input, true);
          } else {
            setTimeout(self2.resume, 3);
          }
        };
        this.aborted = function() {
          return _aborted;
        };
        this.abort = function() {
          _aborted = true;
          _parser.abort();
          _results.meta.aborted = true;
          if (isFunction(_config.complete))
            _config.complete(_results);
          _input = "";
        };
        this.guessLineEndings = function(input, quoteChar) {
          input = input.substring(0, 1024 * 1024);
          var re = new RegExp(escapeRegExp(quoteChar) + "([^]*?)" + escapeRegExp(quoteChar), "gm");
          input = input.replace(re, "");
          var r = input.split("\r");
          var n = input.split("\n");
          var nAppearsFirst = n.length > 1 && n[0].length < r[0].length;
          if (r.length === 1 || nAppearsFirst)
            return "\n";
          var numWithN = 0;
          for (var i = 0; i < r.length; i++) {
            if (r[i][0] === "\n")
              numWithN++;
          }
          return numWithN >= r.length / 2 ? "\r\n" : "\r";
        };
        function testEmptyLine(s) {
          return _config.skipEmptyLines === "greedy" ? s.join("").trim() === "" : s.length === 1 && s[0].length === 0;
        }
        function testFloat(s) {
          if (FLOAT.test(s)) {
            var floatValue = parseFloat(s);
            if (floatValue > MIN_FLOAT && floatValue < MAX_FLOAT) {
              return true;
            }
          }
          return false;
        }
        function processResults() {
          if (_results && _delimiterError) {
            addError("Delimiter", "UndetectableDelimiter", "Unable to auto-detect delimiting character; defaulted to '" + Papa2.DefaultDelimiter + "'");
            _delimiterError = false;
          }
          if (_config.skipEmptyLines) {
            _results.data = _results.data.filter(function(d) {
              return !testEmptyLine(d);
            });
          }
          if (needsHeaderRow())
            fillHeaderFields();
          return applyHeaderAndDynamicTypingAndTransformation();
        }
        function needsHeaderRow() {
          return _config.header && _fields.length === 0;
        }
        function fillHeaderFields() {
          if (!_results)
            return;
          function addHeader(header, i2) {
            header = stripBom(header);
            if (isFunction(_config.transformHeader))
              header = _config.transformHeader(header, i2);
            _fields.push(header);
          }
          if (Array.isArray(_results.data[0])) {
            for (var i = 0; needsHeaderRow() && i < _results.data.length; i++)
              _results.data[i].forEach(addHeader);
            _results.data.splice(0, 1);
          } else
            _results.data.forEach(addHeader);
        }
        function shouldApplyDynamicTyping(field) {
          if (_config.dynamicTypingFunction && _config.dynamicTyping[field] === void 0) {
            _config.dynamicTyping[field] = _config.dynamicTypingFunction(field);
          }
          return (_config.dynamicTyping[field] || _config.dynamicTyping) === true;
        }
        function parseDynamic(field, value) {
          if (shouldApplyDynamicTyping(field)) {
            if (value === "true" || value === "TRUE")
              return true;
            else if (value === "false" || value === "FALSE")
              return false;
            else if (testFloat(value))
              return parseFloat(value);
            else if (ISO_DATE.test(value))
              return new Date(value);
            else
              return value === "" ? null : value;
          }
          return value;
        }
        function applyHeaderAndDynamicTypingAndTransformation() {
          if (!_results || !_config.header && !_config.dynamicTyping && !_config.transform)
            return _results;
          function processRow(rowSource, i) {
            var row = _config.header ? {} : [];
            var j;
            for (j = 0; j < rowSource.length; j++) {
              var field = j;
              var value = rowSource[j];
              if (_config.header)
                field = j >= _fields.length ? "__parsed_extra" : _fields[j];
              if (_config.transform)
                value = _config.transform(value, field);
              value = parseDynamic(field, value);
              if (field === "__parsed_extra") {
                row[field] = row[field] || [];
                row[field].push(value);
              } else
                row[field] = value;
            }
            if (_config.header) {
              if (j > _fields.length)
                addError("FieldMismatch", "TooManyFields", "Too many fields: expected " + _fields.length + " fields but parsed " + j, _rowCounter + i);
              else if (j < _fields.length)
                addError("FieldMismatch", "TooFewFields", "Too few fields: expected " + _fields.length + " fields but parsed " + j, _rowCounter + i);
            }
            return row;
          }
          var incrementBy = 1;
          if (!_results.data.length || Array.isArray(_results.data[0])) {
            _results.data = _results.data.map(processRow);
            incrementBy = _results.data.length;
          } else
            _results.data = processRow(_results.data, 0);
          if (_config.header && _results.meta)
            _results.meta.fields = _fields;
          _rowCounter += incrementBy;
          return _results;
        }
        function guessDelimiter(input, newline, skipEmptyLines, comments, delimitersToGuess) {
          var bestDelim, bestDelta, fieldCountPrevRow, maxFieldCount;
          delimitersToGuess = delimitersToGuess || [",", "	", "|", ";", Papa2.RECORD_SEP, Papa2.UNIT_SEP];
          for (var i = 0; i < delimitersToGuess.length; i++) {
            var delim = delimitersToGuess[i];
            var delta = 0, avgFieldCount = 0, emptyLinesCount = 0;
            fieldCountPrevRow = void 0;
            var preview = new Parser({
              comments,
              delimiter: delim,
              newline,
              preview: 10
            }).parse(input);
            for (var j = 0; j < preview.data.length; j++) {
              if (skipEmptyLines && testEmptyLine(preview.data[j])) {
                emptyLinesCount++;
                continue;
              }
              var fieldCount = preview.data[j].length;
              avgFieldCount += fieldCount;
              if (typeof fieldCountPrevRow === "undefined") {
                fieldCountPrevRow = fieldCount;
                continue;
              } else if (fieldCount > 0) {
                delta += Math.abs(fieldCount - fieldCountPrevRow);
                fieldCountPrevRow = fieldCount;
              }
            }
            if (preview.data.length > 0)
              avgFieldCount /= preview.data.length - emptyLinesCount;
            if ((typeof bestDelta === "undefined" || delta <= bestDelta) && (typeof maxFieldCount === "undefined" || avgFieldCount > maxFieldCount) && avgFieldCount > 1.99) {
              bestDelta = delta;
              bestDelim = delim;
              maxFieldCount = avgFieldCount;
            }
          }
          _config.delimiter = bestDelim;
          return {
            successful: !!bestDelim,
            bestDelimiter: bestDelim
          };
        }
        function addError(type, code, msg, row) {
          var error = {
            type,
            code,
            message: msg
          };
          if (row !== void 0) {
            error.row = row;
          }
          _results.errors.push(error);
        }
      }
      function escapeRegExp(string) {
        return string.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
      }
      function Parser(config) {
        config = config || {};
        var delim = config.delimiter;
        var newline = config.newline;
        var comments = config.comments;
        var step = config.step;
        var preview = config.preview;
        var fastMode = config.fastMode;
        var quoteChar;
        var renamedHeaders = null;
        var headerParsed = false;
        if (config.quoteChar === void 0 || config.quoteChar === null) {
          quoteChar = '"';
        } else {
          quoteChar = config.quoteChar;
        }
        var escapeChar = quoteChar;
        if (config.escapeChar !== void 0) {
          escapeChar = config.escapeChar;
        }
        if (typeof delim !== "string" || Papa2.BAD_DELIMITERS.indexOf(delim) > -1)
          delim = ",";
        if (comments === delim)
          throw new Error("Comment character same as delimiter");
        else if (comments === true)
          comments = "#";
        else if (typeof comments !== "string" || Papa2.BAD_DELIMITERS.indexOf(comments) > -1)
          comments = false;
        if (newline !== "\n" && newline !== "\r" && newline !== "\r\n")
          newline = "\n";
        var cursor = 0;
        var aborted = false;
        this.parse = function(input, baseIndex, ignoreLastRow) {
          if (typeof input !== "string")
            throw new Error("Input must be a string");
          var inputLen = input.length, delimLen = delim.length, newlineLen = newline.length, commentsLen = comments.length;
          var stepIsFunction = isFunction(step);
          cursor = 0;
          var data = [], errors = [], row = [], lastCursor = 0;
          if (!input)
            return returnable();
          if (fastMode || fastMode !== false && input.indexOf(quoteChar) === -1) {
            var rows = input.split(newline);
            for (var i = 0; i < rows.length; i++) {
              row = rows[i];
              cursor += row.length;
              if (i !== rows.length - 1)
                cursor += newline.length;
              else if (ignoreLastRow)
                return returnable();
              if (comments && row.substring(0, commentsLen) === comments)
                continue;
              if (stepIsFunction) {
                data = [];
                pushRow(row.split(delim));
                doStep();
                if (aborted)
                  return returnable();
              } else
                pushRow(row.split(delim));
              if (preview && i >= preview) {
                data = data.slice(0, preview);
                return returnable(true);
              }
            }
            return returnable();
          }
          var nextDelim = input.indexOf(delim, cursor);
          var nextNewline = input.indexOf(newline, cursor);
          var quoteCharRegex = new RegExp(escapeRegExp(escapeChar) + escapeRegExp(quoteChar), "g");
          var quoteSearch = input.indexOf(quoteChar, cursor);
          for (; ; ) {
            if (input[cursor] === quoteChar) {
              quoteSearch = cursor;
              cursor++;
              for (; ; ) {
                quoteSearch = input.indexOf(quoteChar, quoteSearch + 1);
                if (quoteSearch === -1) {
                  if (!ignoreLastRow) {
                    errors.push({
                      type: "Quotes",
                      code: "MissingQuotes",
                      message: "Quoted field unterminated",
                      row: data.length,
                      // row has yet to be inserted
                      index: cursor
                    });
                  }
                  return finish();
                }
                if (quoteSearch === inputLen - 1) {
                  var value = input.substring(cursor, quoteSearch).replace(quoteCharRegex, quoteChar);
                  return finish(value);
                }
                if (quoteChar === escapeChar && input[quoteSearch + 1] === escapeChar) {
                  quoteSearch++;
                  continue;
                }
                if (quoteChar !== escapeChar && quoteSearch !== 0 && input[quoteSearch - 1] === escapeChar) {
                  continue;
                }
                if (nextDelim !== -1 && nextDelim < quoteSearch + 1) {
                  nextDelim = input.indexOf(delim, quoteSearch + 1);
                }
                if (nextNewline !== -1 && nextNewline < quoteSearch + 1) {
                  nextNewline = input.indexOf(newline, quoteSearch + 1);
                }
                var checkUpTo = nextNewline === -1 ? nextDelim : Math.min(nextDelim, nextNewline);
                var spacesBetweenQuoteAndDelimiter = extraSpaces(checkUpTo);
                if (input.substr(quoteSearch + 1 + spacesBetweenQuoteAndDelimiter, delimLen) === delim) {
                  row.push(input.substring(cursor, quoteSearch).replace(quoteCharRegex, quoteChar));
                  cursor = quoteSearch + 1 + spacesBetweenQuoteAndDelimiter + delimLen;
                  if (input[quoteSearch + 1 + spacesBetweenQuoteAndDelimiter + delimLen] !== quoteChar) {
                    quoteSearch = input.indexOf(quoteChar, cursor);
                  }
                  nextDelim = input.indexOf(delim, cursor);
                  nextNewline = input.indexOf(newline, cursor);
                  break;
                }
                var spacesBetweenQuoteAndNewLine = extraSpaces(nextNewline);
                if (input.substring(quoteSearch + 1 + spacesBetweenQuoteAndNewLine, quoteSearch + 1 + spacesBetweenQuoteAndNewLine + newlineLen) === newline) {
                  row.push(input.substring(cursor, quoteSearch).replace(quoteCharRegex, quoteChar));
                  saveRow(quoteSearch + 1 + spacesBetweenQuoteAndNewLine + newlineLen);
                  nextDelim = input.indexOf(delim, cursor);
                  quoteSearch = input.indexOf(quoteChar, cursor);
                  if (stepIsFunction) {
                    doStep();
                    if (aborted)
                      return returnable();
                  }
                  if (preview && data.length >= preview)
                    return returnable(true);
                  break;
                }
                errors.push({
                  type: "Quotes",
                  code: "InvalidQuotes",
                  message: "Trailing quote on quoted field is malformed",
                  row: data.length,
                  // row has yet to be inserted
                  index: cursor
                });
                quoteSearch++;
                continue;
              }
              continue;
            }
            if (comments && row.length === 0 && input.substring(cursor, cursor + commentsLen) === comments) {
              if (nextNewline === -1)
                return returnable();
              cursor = nextNewline + newlineLen;
              nextNewline = input.indexOf(newline, cursor);
              nextDelim = input.indexOf(delim, cursor);
              continue;
            }
            if (nextDelim !== -1 && (nextDelim < nextNewline || nextNewline === -1)) {
              row.push(input.substring(cursor, nextDelim));
              cursor = nextDelim + delimLen;
              nextDelim = input.indexOf(delim, cursor);
              continue;
            }
            if (nextNewline !== -1) {
              row.push(input.substring(cursor, nextNewline));
              saveRow(nextNewline + newlineLen);
              if (stepIsFunction) {
                doStep();
                if (aborted)
                  return returnable();
              }
              if (preview && data.length >= preview)
                return returnable(true);
              continue;
            }
            break;
          }
          return finish();
          function pushRow(row2) {
            data.push(row2);
            lastCursor = cursor;
          }
          function extraSpaces(index) {
            var spaceLength = 0;
            if (index !== -1) {
              var textBetweenClosingQuoteAndIndex = input.substring(quoteSearch + 1, index);
              if (textBetweenClosingQuoteAndIndex && textBetweenClosingQuoteAndIndex.trim() === "") {
                spaceLength = textBetweenClosingQuoteAndIndex.length;
              }
            }
            return spaceLength;
          }
          function finish(value2) {
            if (ignoreLastRow)
              return returnable();
            if (typeof value2 === "undefined")
              value2 = input.substring(cursor);
            row.push(value2);
            cursor = inputLen;
            pushRow(row);
            if (stepIsFunction)
              doStep();
            return returnable();
          }
          function saveRow(newCursor) {
            cursor = newCursor;
            pushRow(row);
            row = [];
            nextNewline = input.indexOf(newline, cursor);
          }
          function returnable(stopped) {
            if (config.header && !baseIndex && data.length && !headerParsed) {
              const result = data[0];
              const headerCount = /* @__PURE__ */ Object.create(null);
              const usedHeaders = new Set(result);
              let duplicateHeaders = false;
              for (let i2 = 0; i2 < result.length; i2++) {
                let header = stripBom(result[i2]);
                if (isFunction(config.transformHeader))
                  header = config.transformHeader(header, i2);
                if (!headerCount[header]) {
                  headerCount[header] = 1;
                  result[i2] = header;
                } else {
                  let newHeader;
                  let suffixCount = headerCount[header];
                  do {
                    newHeader = `${header}_${suffixCount}`;
                    suffixCount++;
                  } while (usedHeaders.has(newHeader));
                  usedHeaders.add(newHeader);
                  result[i2] = newHeader;
                  headerCount[header]++;
                  duplicateHeaders = true;
                  if (renamedHeaders === null) {
                    renamedHeaders = {};
                  }
                  renamedHeaders[newHeader] = header;
                }
                usedHeaders.add(header);
              }
              if (duplicateHeaders) {
                console.warn("Duplicate headers found and renamed.");
              }
              headerParsed = true;
            }
            return {
              data,
              errors,
              meta: {
                delimiter: delim,
                linebreak: newline,
                aborted,
                truncated: !!stopped,
                cursor: lastCursor + (baseIndex || 0),
                renamedHeaders
              }
            };
          }
          function doStep() {
            step(returnable());
            data = [];
            errors = [];
          }
        };
        this.abort = function() {
          aborted = true;
        };
        this.getCharIndex = function() {
          return cursor;
        };
      }
      function newWorker() {
        if (!Papa2.WORKERS_SUPPORTED)
          return false;
        var workerUrl = getWorkerBlob();
        var w = new global.Worker(workerUrl);
        w.onmessage = mainThreadReceivedMessage;
        w.id = workerIdCounter++;
        workers[w.id] = w;
        return w;
      }
      function mainThreadReceivedMessage(e) {
        var msg = e.data;
        var worker = workers[msg.workerId];
        var aborted = false;
        if (msg.error)
          worker.userError(msg.error, msg.file);
        else if (msg.results && msg.results.data) {
          var abort = function() {
            aborted = true;
            completeWorker(msg.workerId, { data: [], errors: [], meta: { aborted: true } });
          };
          var handle = {
            abort,
            pause: notImplemented,
            resume: notImplemented
          };
          if (isFunction(worker.userStep)) {
            for (var i = 0; i < msg.results.data.length; i++) {
              worker.userStep({
                data: msg.results.data[i],
                errors: msg.results.errors,
                meta: msg.results.meta
              }, handle);
              if (aborted)
                break;
            }
            delete msg.results;
          } else if (isFunction(worker.userChunk)) {
            worker.userChunk(msg.results, handle, msg.file);
            delete msg.results;
          }
        }
        if (msg.finished && !aborted)
          completeWorker(msg.workerId, msg.results);
      }
      function completeWorker(workerId, results) {
        var worker = workers[workerId];
        if (isFunction(worker.userComplete))
          worker.userComplete(results);
        worker.terminate();
        delete workers[workerId];
      }
      function notImplemented() {
        throw new Error("Not implemented.");
      }
      function workerThreadReceivedMessage(e) {
        var msg = e.data;
        if (typeof Papa2.WORKER_ID === "undefined" && msg)
          Papa2.WORKER_ID = msg.workerId;
        if (typeof msg.input === "string") {
          global.postMessage({
            workerId: Papa2.WORKER_ID,
            results: Papa2.parse(msg.input, msg.config),
            finished: true
          });
        } else if (global.File && msg.input instanceof File || msg.input instanceof Object) {
          var results = Papa2.parse(msg.input, msg.config);
          if (results)
            global.postMessage({
              workerId: Papa2.WORKER_ID,
              results,
              finished: true
            });
        }
      }
      function copy(obj) {
        if (typeof obj !== "object" || obj === null)
          return obj;
        var cpy = Array.isArray(obj) ? [] : {};
        for (var key in obj)
          cpy[key] = copy(obj[key]);
        return cpy;
      }
      function bindFunction(f, self2) {
        return function() {
          f.apply(self2, arguments);
        };
      }
      function isFunction(func) {
        return typeof func === "function";
      }
      return Papa2;
    });
  }
});

// src/App.jsx
var App_exports = {};
__export(App_exports, {
  default: () => App
});
module.exports = __toCommonJS(App_exports);
var import_react2 = __toESM(require("react"), 1);
var import_papaparse = __toESM(require_papaparse(), 1);
var import_lucide_react2 = require("lucide-react");

// src/components/AutomationBuilder.jsx
var import_react = __toESM(require("react"), 1);
var import_lucide_react = require("lucide-react");
var STEP_TYPES = {
  TRIGGER: {
    label: "Trigger",
    color: "#3B82F6",
    bg: "rgba(59,130,246,0.1)",
    border: "#3B82F6",
    icon: import_lucide_react.Zap,
    category: "Start",
    desc: "When the automation starts"
  },
  WAIT: {
    label: "Wait",
    color: "#F59E0B",
    bg: "rgba(245,158,11,0.1)",
    border: "#F59E0B",
    icon: import_lucide_react.Clock,
    category: "Logic",
    desc: "Pause before continuing"
  },
  CONDITION: {
    label: "Condition",
    color: "#8B5CF6",
    bg: "rgba(139,92,246,0.1)",
    border: "#8B5CF6",
    icon: import_lucide_react.GitBranch,
    category: "Logic",
    desc: "Branch based on contact data"
  },
  WHATSAPP: {
    label: "Send WhatsApp",
    color: "#10B981",
    bg: "rgba(16,185,129,0.1)",
    border: "#10B981",
    icon: import_lucide_react.MessageSquare,
    category: "Actions",
    desc: "Send a WhatsApp message"
  },
  STOP: {
    label: "Stop",
    color: "#EF4444",
    bg: "rgba(239,68,68,0.1)",
    border: "#EF4444",
    icon: import_lucide_react.XCircle,
    category: "Finishers",
    desc: "End the workflow"
  }
};
var TRIGGER_OPTIONS = [
  { value: "PMS_CHECKOUT", label: "PMS Check-out" },
  { value: "PMS_CHECKIN", label: "PMS Check-in" },
  { value: "BOOKING_CREATED", label: "Booking Created" }
];
function StepTypePicker({ onSelect, onClose }) {
  return /* @__PURE__ */ import_react.default.createElement(import_react.default.Fragment, null, /* @__PURE__ */ import_react.default.createElement("div", { style: { position: "fixed", inset: 0, zIndex: 199 }, onClick: onClose }), /* @__PURE__ */ import_react.default.createElement("div", { style: {
    position: "absolute",
    top: 36,
    left: "50%",
    transform: "translateX(-50%)",
    background: "var(--card-bg, #fff)",
    border: "1px solid var(--border, #e2e8f0)",
    borderRadius: 14,
    boxShadow: "0 12px 32px rgba(0,0,0,0.18)",
    zIndex: 200,
    overflow: "hidden",
    width: 220,
    padding: "0.5rem"
  } }, ["Actions", "Logic", "Finishers"].map((cat) => /* @__PURE__ */ import_react.default.createElement("div", { key: cat, style: { marginBottom: "0.5rem" } }, /* @__PURE__ */ import_react.default.createElement("div", { style: { fontSize: "0.6rem", fontWeight: 800, color: "var(--text-sub, #64748b)", padding: "0.25rem 0.75rem", textTransform: "uppercase", letterSpacing: "0.05em" } }, cat), Object.entries(STEP_TYPES).filter(([k, v]) => k !== "TRIGGER" && v.category === cat).map(([key, cfg]) => {
    const Icon = cfg.icon;
    return /* @__PURE__ */ import_react.default.createElement(
      "button",
      {
        key,
        onClick: () => {
          onSelect(key);
          onClose();
        },
        style: {
          width: "100%",
          display: "flex",
          alignItems: "center",
          gap: "0.75rem",
          padding: "0.6rem 0.75rem",
          border: "none",
          background: "none",
          cursor: "pointer",
          textAlign: "left",
          color: "var(--text-heading, #1e293b)",
          fontSize: "0.82rem",
          borderRadius: 8,
          transition: "background .15s"
        },
        onMouseEnter: (e) => e.currentTarget.style.background = "var(--hover, #f1f5f9)",
        onMouseLeave: (e) => e.currentTarget.style.background = "none"
      },
      /* @__PURE__ */ import_react.default.createElement("div", { style: { width: 28, height: 28, borderRadius: 7, background: cfg.bg, display: "flex", alignItems: "center", justifyContent: "center", flexShrink: 0 } }, /* @__PURE__ */ import_react.default.createElement(Icon, { size: 13, color: cfg.color })),
      /* @__PURE__ */ import_react.default.createElement("span", { style: { fontWeight: 500 } }, cfg.label)
    );
  })))));
}
function AddBtn({ onAdd }) {
  const [open, setOpen] = (0, import_react.useState)(false);
  return /* @__PURE__ */ import_react.default.createElement("div", { style: { display: "flex", flexDirection: "column", alignItems: "center", position: "relative", margin: "4px 0" } }, /* @__PURE__ */ import_react.default.createElement("div", { style: { width: 2, height: 18, background: "var(--border, #e2e8f0)" } }), /* @__PURE__ */ import_react.default.createElement("div", { style: { position: "relative" } }, /* @__PURE__ */ import_react.default.createElement(
    "button",
    {
      onClick: () => setOpen((o) => !o),
      title: "Add step",
      style: {
        width: 26,
        height: 26,
        borderRadius: "50%",
        border: "2px dashed var(--border, #e2e8f0)",
        background: "var(--bg, #f8fafc)",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        cursor: "pointer",
        color: "var(--text-sub, #64748b)",
        transition: "all .18s"
      },
      onMouseEnter: (e) => {
        e.currentTarget.style.borderColor = "#6366F1";
        e.currentTarget.style.color = "#6366F1";
        e.currentTarget.style.background = "rgba(99,102,241,0.08)";
      },
      onMouseLeave: (e) => {
        e.currentTarget.style.borderColor = "var(--border, #e2e8f0)";
        e.currentTarget.style.color = "var(--text-sub, #64748b)";
        e.currentTarget.style.background = "var(--bg, #f8fafc)";
      }
    },
    /* @__PURE__ */ import_react.default.createElement(import_lucide_react.Plus, { size: 12 })
  ), open && /* @__PURE__ */ import_react.default.createElement(StepTypePicker, { onSelect: onAdd, onClose: () => setOpen(false) })), /* @__PURE__ */ import_react.default.createElement("div", { style: { width: 2, height: 18, background: "var(--border, #e2e8f0)" } }));
}
function StepCard({ step, onUpdate, onDelete, canDelete = true, allTriggerOptions = [] }) {
  const [isExpanded, setIsExpanded] = (0, import_react.useState)(true);
  const cfg = STEP_TYPES[step.stepType] || STEP_TYPES.WHATSAPP;
  const Icon = cfg.icon;
  return /* @__PURE__ */ import_react.default.createElement("div", { style: {
    border: `2px solid ${cfg.border}`,
    borderRadius: 14,
    background: "var(--card-bg, #ffffff)",
    overflow: "hidden",
    boxShadow: "0 2px 14px rgba(0,0,0,0.07)",
    width: "100%",
    maxWidth: "400px",
    margin: "0 auto"
  } }, /* @__PURE__ */ import_react.default.createElement("div", { style: {
    background: cfg.bg,
    padding: "0.7rem 0.9rem",
    display: "flex",
    alignItems: "center",
    gap: "0.65rem",
    cursor: "pointer",
    userSelect: "none"
  }, onClick: () => setIsExpanded((e) => !e) }, /* @__PURE__ */ import_react.default.createElement("div", { style: { width: 32, height: 32, borderRadius: 8, background: `${cfg.color}22`, display: "flex", alignItems: "center", justifyContent: "center", flexShrink: 0 } }, /* @__PURE__ */ import_react.default.createElement(Icon, { size: 15, color: cfg.color })), /* @__PURE__ */ import_react.default.createElement("div", { style: { flex: 1, minWidth: 0 } }, /* @__PURE__ */ import_react.default.createElement("div", { style: { fontSize: "0.78rem", fontWeight: 700, color: cfg.color } }, cfg.label), /* @__PURE__ */ import_react.default.createElement("div", { style: { fontSize: "0.68rem", color: "var(--text-sub, #64748b)", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" } }, step.stepType === "TRIGGER" && (allTriggerOptions.find((o) => o.value === step.triggerValue)?.label || "Manual"), step.stepType === "WAIT" && `Wait ${step.waitValue || 1} ${step.waitUnit || "HOURS"}`, step.stepType === "WHATSAPP" && (step.templateId ? `Template: ${step.templateId}` : "No template selected"), step.stepType === "CONDITION" && "Check contact conditions", step.stepType === "STOP" && "Workflow ends")), /* @__PURE__ */ import_react.default.createElement("div", { style: { display: "flex", gap: "0.2rem", flexShrink: 0 } }, isExpanded ? /* @__PURE__ */ import_react.default.createElement(import_lucide_react.ChevronUp, { size: 13, color: "var(--text-sub, #64748b)" }) : /* @__PURE__ */ import_react.default.createElement(import_lucide_react.ChevronDown, { size: 13, color: "var(--text-sub, #64748b)" }), canDelete && step.stepType !== "TRIGGER" && /* @__PURE__ */ import_react.default.createElement(
    "button",
    {
      style: { background: "none", border: "none", cursor: "pointer", padding: 2, color: "#EF4444", lineHeight: 1, borderRadius: 4, transition: "background .15s" },
      onClick: (e) => {
        e.stopPropagation();
        onDelete(step.id);
      },
      title: "Delete step",
      onMouseEnter: (e) => e.currentTarget.style.background = "rgba(239,68,68,0.1)",
      onMouseLeave: (e) => e.currentTarget.style.background = "none"
    },
    /* @__PURE__ */ import_react.default.createElement(import_lucide_react.Trash2, { size: 13 })
  ))), isExpanded && /* @__PURE__ */ import_react.default.createElement("div", { style: { padding: "0.85rem" } }, step.stepType === "TRIGGER" && /* @__PURE__ */ import_react.default.createElement("div", { style: { margin: 0, display: "flex", flexDirection: "column", gap: "0.5rem" } }, /* @__PURE__ */ import_react.default.createElement("label", { style: { fontSize: "0.75rem", fontWeight: 500 } }, "Trigger Event"), /* @__PURE__ */ import_react.default.createElement(
    "select",
    {
      style: { padding: "6px 8px", borderRadius: "6px", border: "1px solid #e2e8f0", fontSize: "0.82rem" },
      value: step.triggerValue || "PMS_CHECKIN",
      onChange: (e) => onUpdate(step.id, { triggerValue: e.target.value })
    },
    allTriggerOptions.map((o) => /* @__PURE__ */ import_react.default.createElement("option", { key: o.value, value: o.value }, o.label))
  ), step.triggerValue && step.triggerValue.startsWith("DATE_") && /* @__PURE__ */ import_react.default.createElement("div", { style: { marginTop: "8px", padding: "12px", background: "#f8fafc", borderRadius: "8px", border: "1px solid #e2e8f0" } }, /* @__PURE__ */ import_react.default.createElement("label", { style: { fontSize: "0.75rem", fontWeight: 600, display: "block", marginBottom: "8px", color: "#0f172a" } }, "Time Offset (Days Before)"), /* @__PURE__ */ import_react.default.createElement("div", { style: { display: "flex", gap: "8px", alignItems: "center" } }, /* @__PURE__ */ import_react.default.createElement(
    "input",
    {
      type: "number",
      value: step.offsetDays || 0,
      onChange: (e) => onUpdate(step.id, { offsetDays: e.target.value }),
      style: { width: "80px", padding: "6px", borderRadius: "4px", border: "1px solid #ccc" }
    }
  ), /* @__PURE__ */ import_react.default.createElement("span", { style: { fontSize: "0.75rem", color: "#64748b" } }, "days before the date")), /* @__PURE__ */ import_react.default.createElement("label", { style: { display: "flex", alignItems: "center", gap: "8px", marginTop: "12px", fontSize: "0.75rem", color: "#475569" } }, /* @__PURE__ */ import_react.default.createElement("input", { type: "checkbox", checked: step.ignoreYear !== false, onChange: (e) => onUpdate(step.id, { ignoreYear: e.target.checked }) }), "Ignore Year (Repeats annually)")), /* @__PURE__ */ import_react.default.createElement("div", { style: { marginTop: "16px", paddingTop: "16px", borderTop: "1px dashed #e2e8f0", display: "flex", flexDirection: "column", gap: "0.5rem" } }, /* @__PURE__ */ import_react.default.createElement("label", { style: { fontSize: "0.75rem", fontWeight: 500 } }, "Send To (Audience Filter)"), /* @__PURE__ */ import_react.default.createElement(
    "select",
    {
      style: { padding: "6px 8px", borderRadius: "6px", border: "1px solid #e2e8f0", fontSize: "0.82rem" },
      value: step.audienceFilter || "All",
      onChange: (e) => onUpdate(step.id, { audienceFilter: e.target.value })
    },
    /* @__PURE__ */ import_react.default.createElement("option", { value: "All" }, "All Contacts"),
    /* @__PURE__ */ import_react.default.createElement("option", { value: "Custom" }, "Custom Column Match")
  ), step.audienceFilter === "Custom" && /* @__PURE__ */ import_react.default.createElement("div", { style: { display: "flex", gap: "8px", marginTop: "4px" } }, /* @__PURE__ */ import_react.default.createElement(
    "input",
    {
      type: "text",
      placeholder: "Column name (e.g. City)",
      style: { flex: 1, padding: "6px 8px", borderRadius: "6px", border: "1px solid #e2e8f0", fontSize: "0.82rem" },
      value: step.audienceCustomColumn || "",
      onChange: (e) => onUpdate(step.id, { audienceCustomColumn: e.target.value })
    }
  ), /* @__PURE__ */ import_react.default.createElement("span", { style: { display: "flex", alignItems: "center", fontSize: "0.8rem", color: "#64748b" } }, "="), /* @__PURE__ */ import_react.default.createElement(
    "input",
    {
      type: "text",
      placeholder: "Value (e.g. Toronto)",
      style: { flex: 1, padding: "6px 8px", borderRadius: "6px", border: "1px solid #e2e8f0", fontSize: "0.82rem" },
      value: step.audienceCustomValue || "",
      onChange: (e) => onUpdate(step.id, { audienceCustomValue: e.target.value })
    }
  )))), step.stepType === "WAIT" && /* @__PURE__ */ import_react.default.createElement("div", { style: { display: "flex", gap: "0.5rem" } }, /* @__PURE__ */ import_react.default.createElement("div", { style: { flex: 1, display: "flex", flexDirection: "column", gap: "0.5rem" } }, /* @__PURE__ */ import_react.default.createElement("label", { style: { fontSize: "0.75rem", fontWeight: 500 } }, "Wait Value"), /* @__PURE__ */ import_react.default.createElement(
    "input",
    {
      type: "number",
      style: { padding: "6px 8px", borderRadius: "6px", border: "1px solid #e2e8f0", fontSize: "0.82rem" },
      value: step.waitValue || 1,
      onChange: (e) => onUpdate(step.id, { waitValue: e.target.value })
    }
  )), /* @__PURE__ */ import_react.default.createElement("div", { style: { flex: 1, display: "flex", flexDirection: "column", gap: "0.5rem" } }, /* @__PURE__ */ import_react.default.createElement("label", { style: { fontSize: "0.75rem", fontWeight: 500 } }, "Unit"), /* @__PURE__ */ import_react.default.createElement(
    "select",
    {
      style: { padding: "6px 8px", borderRadius: "6px", border: "1px solid #e2e8f0", fontSize: "0.82rem" },
      value: step.waitUnit || "HOURS",
      onChange: (e) => onUpdate(step.id, { waitUnit: e.target.value })
    },
    /* @__PURE__ */ import_react.default.createElement("option", { value: "MINUTES" }, "Minutes"),
    /* @__PURE__ */ import_react.default.createElement("option", { value: "HOURS" }, "Hours"),
    /* @__PURE__ */ import_react.default.createElement("option", { value: "DAYS" }, "Days")
  ))), step.stepType === "WHATSAPP" && /* @__PURE__ */ import_react.default.createElement("div", { style: { display: "flex", flexDirection: "column", gap: "0.5rem" } }, /* @__PURE__ */ import_react.default.createElement("label", { style: { fontSize: "0.75rem", fontWeight: 500 } }, "Message Template"), /* @__PURE__ */ import_react.default.createElement(
    "select",
    {
      style: { padding: "6px 8px", borderRadius: "6px", border: "1px solid #e2e8f0", fontSize: "0.82rem" },
      value: step.templateId || "",
      onChange: (e) => onUpdate(step.id, { templateId: e.target.value })
    },
    /* @__PURE__ */ import_react.default.createElement("option", { value: "" }, "-- Select Template --"),
    step.templatesList?.map((t) => /* @__PURE__ */ import_react.default.createElement("option", { key: t.Id || t.id, value: t.Id || t.id }, t.Name || t.name))
  ), step.templateId && step.templatesList && /* @__PURE__ */ import_react.default.createElement("div", { style: { marginTop: "8px", padding: "12px", background: "#dcf8c6", borderRadius: "12px 12px 12px 0", alignSelf: "flex-start", maxWidth: "90%", position: "relative", boxShadow: "0 1px 1px rgba(0,0,0,0.1)" } }, /* @__PURE__ */ import_react.default.createElement("div", { style: { fontSize: "0.85rem", color: "#000", whiteSpace: "pre-wrap", lineHeight: "1.4" } }, step.templatesList.find((t) => (t.Id || t.id).toString() === step.templateId)?.BodyText || "Loading preview..."), /* @__PURE__ */ import_react.default.createElement("div", { style: { position: "absolute", bottom: "4px", right: "8px", fontSize: "0.6rem", color: "rgba(0,0,0,0.45)" } }, "Preview"))), step.stepType === "CONDITION" && /* @__PURE__ */ import_react.default.createElement("div", { style: { display: "flex", flexDirection: "column", gap: "0.5rem" } }, /* @__PURE__ */ import_react.default.createElement("label", { style: { fontSize: "0.75rem", fontWeight: 500 } }, "Field condition"), /* @__PURE__ */ import_react.default.createElement("p", { style: { fontSize: "0.7rem", color: "#64748b", margin: 0 } }, "This allows splitting the journey based on contact parameters."), /* @__PURE__ */ import_react.default.createElement("div", { style: { display: "flex", gap: "0.5rem" } }, /* @__PURE__ */ import_react.default.createElement(
    "select",
    {
      style: { flex: 1, padding: "6px 8px", borderRadius: "6px", border: "1px solid #e2e8f0", fontSize: "0.82rem" },
      value: step.conditionField || "status",
      onChange: (e) => onUpdate(step.id, { conditionField: e.target.value })
    },
    /* @__PURE__ */ import_react.default.createElement("option", { value: "status" }, "Booking Status"),
    /* @__PURE__ */ import_react.default.createElement("option", { value: "vip" }, "Is VIP")
  ), /* @__PURE__ */ import_react.default.createElement(
    "input",
    {
      style: { flex: 1, padding: "6px 8px", borderRadius: "6px", border: "1px solid #e2e8f0", fontSize: "0.82rem" },
      placeholder: "Value...",
      value: step.conditionValue || "",
      onChange: (e) => onUpdate(step.id, { conditionValue: e.target.value })
    }
  ))), step.stepType === "STOP" && /* @__PURE__ */ import_react.default.createElement("p", { style: { fontSize: "0.75rem", color: "#64748b", margin: 0 } }, "Contacts reaching this step will exit the workflow.")));
}
function AutomationBuilder({ onCancel, initialRule }) {
  const [ruleName, setRuleName] = (0, import_react.useState)(initialRule ? initialRule.Name || initialRule.name || "New WhatsApp Rule" : "New WhatsApp Rule");
  const [ruleDescription, setRuleDescription] = (0, import_react.useState)(initialRule ? initialRule.Description || initialRule.description || "" : "");
  const [dynamicHeaders, setDynamicHeaders] = import_react.default.useState([]);
  const [templates, setTemplates] = import_react.default.useState([]);
  const [steps, setSteps] = (0, import_react.useState)(() => {
    if (initialRule) {
      const isDate = initialRule.TriggerEventType === "DateTrigger";
      let triggerVal = isDate ? "PMS_CHECKIN" : initialRule.TriggerEventType;
      let offsetDays = 0;
      let ignoreYear = true;
      if (isDate && initialRule.ConditionsJson) {
        try {
          const cond = JSON.parse(initialRule.ConditionsJson);
          if (cond.Column) {
            triggerVal = `DATE_${cond.Column}`;
          }
          if (cond.OffsetDays !== void 0) offsetDays = cond.OffsetDays;
          if (cond.IgnoreYear !== void 0) ignoreYear = cond.IgnoreYear;
        } catch (e) {
        }
      }
      let audienceFilter = "All";
      let audienceCustomColumn = "";
      let audienceCustomValue = "";
      if (initialRule.AudienceFilter || initialRule.audienceFilter) {
        try {
          const af = JSON.parse(initialRule.AudienceFilter || initialRule.audienceFilter);
          if (af.Type) audienceFilter = af.Type;
          if (af.Column) audienceCustomColumn = af.Column;
          if (af.Value) audienceCustomValue = af.Value;
        } catch (e) {
        }
      }
      return [
        {
          id: "1",
          stepType: "TRIGGER",
          triggerValue: triggerVal,
          stepOrder: 0,
          offsetDays,
          ignoreYear,
          audienceFilter,
          audienceCustomColumn,
          audienceCustomValue
        },
        {
          id: "2",
          stepType: "WHATSAPP",
          templateId: (initialRule.TemplateId || initialRule.templateId)?.toString(),
          stepOrder: 1,
          templatesList: []
          // will be populated by fetch
        }
      ];
    }
    return [
      { id: "1", stepType: "TRIGGER", triggerValue: "PMS_CHECKIN", stepOrder: 0, audienceFilter: "All" }
    ];
  });
  import_react.default.useEffect(() => {
    fetch("/api/contacts/columns/dates").then((r) => r.json()).then((d) => {
      const uniqueKeys = /* @__PURE__ */ new Map();
      (d || []).forEach((k) => {
        const lower = k.toLowerCase();
        if (!uniqueKeys.has(lower)) {
          uniqueKeys.set(lower, k);
        }
      });
      setDynamicHeaders(Array.from(uniqueKeys.values()));
    }).catch((e) => console.log(e));
    fetch("/api/templates").then((r) => r.json()).then((d) => {
      const tpl = d || [];
      setTemplates(tpl);
      setSteps((prev) => prev.map((s) => s.stepType === "WHATSAPP" ? { ...s, templatesList: tpl } : s));
    }).catch((e) => console.log(e));
  }, []);
  const allTriggerOptions = [
    ...TRIGGER_OPTIONS,
    ...dynamicHeaders.map((h) => ({ value: `DATE_${h}`, label: `Date: ${h}` }))
  ];
  const addStep = (stepType, index) => {
    const newStep = {
      id: Date.now().toString(),
      stepType,
      stepOrder: index + 1,
      templatesList: templates
    };
    setSteps((prev) => {
      const newSteps = [...prev];
      newSteps.splice(index + 1, 0, newStep);
      return newSteps.map((s, i) => ({ ...s, stepOrder: i }));
    });
  };
  const updateStep = (id, updates) => {
    setSteps((prev) => prev.map((s) => s.id === id ? { ...s, ...updates } : s));
  };
  const deleteStep = (id) => {
    setSteps((prev) => {
      const newSteps = prev.filter((s) => s.id !== id);
      return newSteps.map((s, i) => ({ ...s, stepOrder: i }));
    });
  };
  const saveRule = async () => {
    const triggerStep = steps.find((s) => s.stepType === "TRIGGER");
    const actionStep = steps.find((s) => s.stepType === "WHATSAPP");
    if (!triggerStep || !actionStep || !actionStep.templateId) {
      alert("You need at least a Trigger and a Send WhatsApp step with a selected template.");
      return;
    }
    let triggerType = triggerStep.triggerValue || "PMS_CHECKIN";
    let conditionsJson = null;
    if (triggerType.startsWith("DATE_")) {
      const column = triggerType.replace("DATE_", "");
      triggerType = "DateTrigger";
      conditionsJson = JSON.stringify({
        Column: column,
        OffsetDays: parseInt(triggerStep.offsetDays || 0),
        IgnoreYear: triggerStep.ignoreYear !== false
      });
    }
    let audienceFilterJson = null;
    if (triggerStep.audienceFilter === "Custom") {
      audienceFilterJson = JSON.stringify({
        Type: "Custom",
        Column: triggerStep.audienceCustomColumn || "",
        Value: triggerStep.audienceCustomValue || ""
      });
    }
    const rule = {
      Id: initialRule ? initialRule.Id || initialRule.id : void 0,
      Name: ruleName,
      Description: ruleDescription,
      TriggerEventType: triggerType,
      TemplateId: parseInt(actionStep.templateId),
      IsEnabled: true,
      ConditionsJson: conditionsJson,
      AudienceFilter: audienceFilterJson
    };
    try {
      const res = await fetch(initialRule ? `/api/rules/${initialRule.Id}` : "/api/rules", {
        method: initialRule ? "PUT" : "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(rule)
      });
      if (!res.ok) throw new Error();
      alert(`Saved Rule: ${ruleName}`);
      if (onCancel) {
        onCancel();
        window.location.reload();
      }
    } catch (e) {
      alert("Error saving rule");
    }
  };
  return /* @__PURE__ */ import_react.default.createElement("div", { style: { padding: "24px", maxWidth: "1000px", margin: "0 auto", fontFamily: "Inter, sans-serif" } }, /* @__PURE__ */ import_react.default.createElement("div", { style: { display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "24px", background: "#fff", padding: "16px 24px", borderRadius: "12px", boxShadow: "0 4px 6px -1px rgba(0,0,0,0.05)" } }, /* @__PURE__ */ import_react.default.createElement("div", { style: { display: "flex", flexDirection: "column", gap: "8px" } }, /* @__PURE__ */ import_react.default.createElement("div", { style: { display: "flex", alignItems: "center", gap: "12px" } }, /* @__PURE__ */ import_react.default.createElement("button", { onClick: onCancel, style: { background: "none", border: "none", cursor: "pointer", display: "flex", alignItems: "center", color: "#64748b" } }, /* @__PURE__ */ import_react.default.createElement(import_lucide_react.XCircle, { size: 20 })), /* @__PURE__ */ import_react.default.createElement(
    "input",
    {
      type: "text",
      value: ruleName,
      onChange: (e) => setRuleName(e.target.value),
      placeholder: "Rule Name",
      style: { fontSize: "1.25rem", fontWeight: 700, border: "none", outline: "none", background: "transparent" }
    }
  )), /* @__PURE__ */ import_react.default.createElement("div", { style: { marginLeft: "32px" } }, /* @__PURE__ */ import_react.default.createElement(
    "input",
    {
      type: "text",
      value: ruleDescription,
      onChange: (e) => setRuleDescription(e.target.value),
      placeholder: "Add a description for this automation (e.g. Sends birthday promo 7 days early)",
      style: { fontSize: "0.85rem", color: "#64748b", border: "none", outline: "none", background: "transparent", width: "400px" }
    }
  ))), /* @__PURE__ */ import_react.default.createElement("div", { style: { display: "flex", gap: "12px" } }, /* @__PURE__ */ import_react.default.createElement(
    "button",
    {
      className: "btn btn-primary",
      style: { display: "flex", alignItems: "center", gap: "8px", padding: "8px 16px", background: "#3b82f6", color: "#fff", borderRadius: "8px", border: "none", cursor: "pointer", fontWeight: 500 },
      onClick: saveRule
    },
    /* @__PURE__ */ import_react.default.createElement(import_lucide_react.Save, { size: 16 }),
    " Save Rule"
  ))), /* @__PURE__ */ import_react.default.createElement("div", { style: { display: "flex", flexDirection: "column", alignItems: "center", padding: "32px 0" } }, steps.map((step, index) => /* @__PURE__ */ import_react.default.createElement(import_react.default.Fragment, { key: step.id }, /* @__PURE__ */ import_react.default.createElement(
    StepCard,
    {
      step,
      onUpdate: updateStep,
      onDelete: deleteStep,
      canDelete: step.stepType !== "TRIGGER",
      allTriggerOptions
    }
  ), index < steps.length - 1 && /* @__PURE__ */ import_react.default.createElement(AddBtn, { onAdd: (type) => addStep(type, index) }))), /* @__PURE__ */ import_react.default.createElement(AddBtn, { onAdd: (type) => addStep(type, steps.length - 1) }), /* @__PURE__ */ import_react.default.createElement("div", { style: {
    width: 48,
    height: 48,
    borderRadius: "50%",
    border: "2px dashed #cbd5e1",
    display: "flex",
    alignItems: "center",
    justifyContent: "center",
    color: "#cbd5e1",
    marginTop: "16px"
  } }, /* @__PURE__ */ import_react.default.createElement(import_lucide_react.CheckCircle2, { size: 24 }))));
}

// src/App.jsx
var API_BASE = "/api";
var formatDateValue = (val) => {
  if (!val) return "-";
  if (typeof val === "string") {
    const isoDateRegex = /^(\d{4})[-\/](\d{2})[-\/](\d{2})$/;
    const match = val.trim().match(isoDateRegex);
    if (match) {
      return `${match[3]}-${match[2]}-${match[1]}`;
    }
  }
  return val;
};
function App() {
  const [activeTab, setActiveTab] = (0, import_react2.useState)(() => localStorage.getItem("hotelAutoActiveTab") || "dashboard");
  (0, import_react2.useEffect)(() => {
    localStorage.setItem("hotelAutoActiveTab", activeTab);
  }, [activeTab]);
  const [stats, setStats] = (0, import_react2.useState)({ templates: 0, rules: 0, outbox: 0, inbox: 0 });
  const [outbox, setOutbox] = (0, import_react2.useState)([]);
  const [inbox, setInbox] = (0, import_react2.useState)([]);
  const [isBuildingRule, setIsBuildingRule] = (0, import_react2.useState)(false);
  const [editingRule, setEditingRule] = (0, import_react2.useState)(null);
  const [categories, setCategories] = (0, import_react2.useState)([]);
  const [contacts, setContacts] = (0, import_react2.useState)([]);
  const [totalContacts, setTotalContacts] = (0, import_react2.useState)(0);
  const [currentPage, setCurrentPage] = (0, import_react2.useState)(1);
  const [pageSize, setPageSize] = (0, import_react2.useState)(50);
  const [dynamicHeaders, setDynamicHeaders] = (0, import_react2.useState)([]);
  const [showEditContactModal, setShowEditContactModal] = (0, import_react2.useState)(false);
  const [editingContactId, setEditingContactId] = (0, import_react2.useState)(null);
  const [editContactData, setEditContactData] = (0, import_react2.useState)({ PhoneNumber: "", FirstName: "", LastName: "", CustomFieldsJson: "{}" });
  const [logs, setLogs] = (0, import_react2.useState)([]);
  const [templatesList, setTemplatesList] = (0, import_react2.useState)([]);
  const [rulesList, setRulesList] = (0, import_react2.useState)([]);
  const [broadcasts, setBroadcasts] = (0, import_react2.useState)([]);
  const [selectedBroadcastCategory, setSelectedBroadcastCategory] = (0, import_react2.useState)("");
  const [selectedBroadcastTemplate, setSelectedBroadcastTemplate] = (0, import_react2.useState)("");
  const [selectedCategory, setSelectedCategory] = (0, import_react2.useState)("");
  const [isFiring, setIsFiring] = (0, import_react2.useState)(false);
  const [isImporting, setIsImporting] = (0, import_react2.useState)(false);
  const [testPhoneNumber, setTestPhoneNumber] = (0, import_react2.useState)("+15556585587");
  const [showTemplateModal, setShowTemplateModal] = (0, import_react2.useState)(false);
  const [showColumnModal, setShowColumnModal] = (0, import_react2.useState)(false);
  const [importModalOpen, setImportModalOpen] = (0, import_react2.useState)(false);
  const [previewData, setPreviewData] = (0, import_react2.useState)([]);
  const [previewHeaders, setPreviewHeaders] = (0, import_react2.useState)([]);
  const [importTotalRows, setImportTotalRows] = (0, import_react2.useState)(0);
  const [importProgress, setImportProgress] = (0, import_react2.useState)(0);
  const [parsedContacts, setParsedContacts] = (0, import_react2.useState)([]);
  const [isEditingTemplate, setIsEditingTemplate] = (0, import_react2.useState)(false);
  const [editingTemplateId, setEditingTemplateId] = (0, import_react2.useState)(null);
  const [newTemplate, setNewTemplate] = (0, import_react2.useState)({ Name: "", ExternalTemplateName: "", LanguageCode: "en", ChannelType: "WhatsApp" });
  (0, import_react2.useEffect)(() => {
    fetchData();
  }, [activeTab, currentPage, pageSize]);
  const fetchData = async () => {
    try {
      const outboxRes = await fetch(`${API_BASE}/outbox`).catch(() => null);
      if (outboxRes) {
        const outboxData = await outboxRes.json();
        setOutbox(outboxData);
        setStats((s) => ({ ...s, outbox: outboxData.length }));
      }
      if (activeTab === "templates" || activeTab === "broadcasts") {
        const templatesRes = await fetch(`${API_BASE}/templates`).catch(() => null);
        if (templatesRes) setTemplatesList(await templatesRes.json());
      }
      const rulesRes = await fetch(`${API_BASE}/rules`).catch(() => null);
      if (rulesRes) {
        const rulesData = await rulesRes.json();
        setRulesList(rulesData);
        setStats((s) => ({ ...s, rules: rulesData.length }));
      }
      if (activeTab === "logs") {
        const logsRes = await fetch(`${API_BASE}/logs`).catch(() => null);
        if (logsRes) setLogs(await logsRes.json());
      }
      if (activeTab === "broadcasts") {
        const bRes = await fetch(`${API_BASE}/broadcast`).catch(() => null);
        if (bRes) setBroadcasts(await bRes.json());
        if (categories.length === 0) {
          const catRes2 = await fetch(`${API_BASE}/categories`).catch(() => null);
          if (catRes2) setCategories(await catRes2.json());
        }
      }
      const inboxRes = await fetch(`${API_BASE}/inbox`).catch(() => null);
      if (inboxRes) {
        const inboxData = await inboxRes.json();
        setInbox(inboxData);
        setStats((s) => ({ ...s, inbox: inboxData.length }));
      }
      const catRes = await fetch(`${API_BASE}/categories`).catch(() => null);
      if (catRes) setCategories(await catRes.json());
      const conUrl = selectedCategory ? `${API_BASE}/contacts?categoryId=${selectedCategory}&page=${currentPage}&limit=${pageSize}` : `${API_BASE}/contacts?page=${currentPage}&limit=${pageSize}`;
      const conRes = await fetch(conUrl).catch(() => null);
      if (conRes) {
        const data = await conRes.json();
        setContacts(data.Contacts || data.contacts || []);
        setTotalContacts(data.TotalCount || data.totalCount || 0);
      }
      const colRes = await fetch(`${API_BASE}/contacts/columns/all`).catch(() => null);
      if (colRes) {
        setDynamicHeaders(await colRes.json());
      }
    } catch (e) {
      console.error(e);
    }
  };
  const handleTestEvent = async () => {
    setIsFiring(true);
    try {
      await fetch(`${API_BASE}/test-event?phone=${encodeURIComponent(testPhoneNumber)}`, { method: "POST" });
      await new Promise((r) => setTimeout(r, 1e3));
      await fetchData();
    } catch (e) {
      alert("Ensure the C# Backend API is running!");
    }
    setIsFiring(false);
  };
  const handleTestReply = async () => {
    try {
      await fetch(`${API_BASE}/webhooks/whatsapp`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ From: "+1234567890", Body: "Yes, please book the spa!" })
      });
      await new Promise((r) => setTimeout(r, 1e3));
      await fetchData();
    } catch (e) {
      alert("Ensure the C# Backend API is running!");
    }
  };
  const handleSimulateWebhook = async (status) => {
    const msg = outbox.find((m) => m.Status === "Sent" || m.status === "Sent" || m.Status === "Delivered" || m.status === "Delivered");
    if (!msg) {
      alert("No suitable Sent/Delivered message found in outbox!");
      return;
    }
    const wamid = msg.ExternalMessageId || msg.externalMessageId || msg.Id || msg.id;
    try {
      await fetch(`${API_BASE}/test-webhook?wamid=${encodeURIComponent(wamid)}&status=${status}`, { method: "POST" });
      await new Promise((r) => setTimeout(r, 1e3));
      await fetchData();
    } catch (e) {
      alert("Ensure the C# Backend API is running!");
    }
  };
  const renderContent = () => {
    if (activeTab === "dashboard") {
      return /* @__PURE__ */ import_react2.default.createElement("div", null, /* @__PURE__ */ import_react2.default.createElement("div", { style: { display: "flex", gap: "24px", marginBottom: "32px" } }, /* @__PURE__ */ import_react2.default.createElement("div", { className: "card", style: { flex: 1 } }, /* @__PURE__ */ import_react2.default.createElement("div", { style: { color: "var(--text-secondary)", marginBottom: "8px" } }, "Total Messages"), /* @__PURE__ */ import_react2.default.createElement("div", { style: { fontSize: "32px", fontWeight: "bold" } }, stats.outbox)), /* @__PURE__ */ import_react2.default.createElement("div", { className: "card", style: { flex: 1 } }, /* @__PURE__ */ import_react2.default.createElement("div", { style: { color: "var(--text-secondary)", marginBottom: "8px" } }, "Active Rules"), /* @__PURE__ */ import_react2.default.createElement("div", { style: { fontSize: "32px", fontWeight: "bold" } }, stats.rules)), /* @__PURE__ */ import_react2.default.createElement("div", { className: "card", style: { flex: 1 } }, /* @__PURE__ */ import_react2.default.createElement("div", { style: { color: "var(--text-secondary)", marginBottom: "8px" } }, "Inbox Replies"), /* @__PURE__ */ import_react2.default.createElement("div", { style: { fontSize: "32px", fontWeight: "bold" } }, stats.inbox))), /* @__PURE__ */ import_react2.default.createElement("div", { style: { display: "flex", gap: "24px", marginBottom: "32px" } }, /* @__PURE__ */ import_react2.default.createElement("div", { className: "card", style: { flex: 1, display: "flex", alignItems: "center", justifyContent: "space-between" } }, /* @__PURE__ */ import_react2.default.createElement("div", null, /* @__PURE__ */ import_react2.default.createElement("h3", { style: { marginBottom: "8px" } }, "PMS CheckOut Simulation"), /* @__PURE__ */ import_react2.default.createElement("p", { style: { color: "var(--text-secondary)" } }, "Fires event for the given number. Starts the marketing drip campaign."), /* @__PURE__ */ import_react2.default.createElement(
        "input",
        {
          type: "text",
          value: testPhoneNumber,
          onChange: (e) => setTestPhoneNumber(e.target.value),
          style: { marginTop: "8px", padding: "8px", width: "200px", borderRadius: "4px", border: "1px solid #ccc", color: "#000" }
        }
      )), /* @__PURE__ */ import_react2.default.createElement("button", { className: "btn btn-primary", onClick: handleTestEvent, disabled: isFiring }, /* @__PURE__ */ import_react2.default.createElement(import_lucide_react2.Send, { size: 18 }), " Trigger Event")), /* @__PURE__ */ import_react2.default.createElement("div", { className: "card", style: { flex: 1, display: "flex", alignItems: "center", justifyContent: "space-between" } }, /* @__PURE__ */ import_react2.default.createElement("div", null, /* @__PURE__ */ import_react2.default.createElement("h3", { style: { marginBottom: "8px" } }, "Analytics Webhook Simulator"), /* @__PURE__ */ import_react2.default.createElement("p", { style: { color: "var(--text-secondary)" } }, "Simulates Meta sending Delivery/Read receipts.")), /* @__PURE__ */ import_react2.default.createElement("div", { style: { display: "flex", gap: "8px" } }, /* @__PURE__ */ import_react2.default.createElement("button", { className: "btn btn-primary", onClick: () => handleSimulateWebhook("Delivered"), style: { backgroundColor: "#10b981" } }, "Simulate Delivered"), /* @__PURE__ */ import_react2.default.createElement("button", { className: "btn btn-primary", onClick: () => handleSimulateWebhook("Read"), style: { backgroundColor: "#3b82f6" } }, "Simulate Read")))), /* @__PURE__ */ import_react2.default.createElement("div", { style: { display: "flex", gap: "24px" } }, /* @__PURE__ */ import_react2.default.createElement("div", { className: "card", style: { flex: 1 } }, /* @__PURE__ */ import_react2.default.createElement("h3", { className: "card-title" }, "Recent Outbox"), outbox.length === 0 ? /* @__PURE__ */ import_react2.default.createElement("div", { className: "empty-state" }, "No outbox messages found.") : /* @__PURE__ */ import_react2.default.createElement("div", { className: "data-table-container" }, /* @__PURE__ */ import_react2.default.createElement("table", { className: "data-table" }, /* @__PURE__ */ import_react2.default.createElement("thead", null, /* @__PURE__ */ import_react2.default.createElement("tr", null, /* @__PURE__ */ import_react2.default.createElement("th", null, "Phone Number"), /* @__PURE__ */ import_react2.default.createElement("th", null, "Status"))), /* @__PURE__ */ import_react2.default.createElement("tbody", null, (outbox || []).map((msg) => /* @__PURE__ */ import_react2.default.createElement("tr", { key: msg.Id || msg.id }, /* @__PURE__ */ import_react2.default.createElement("td", { style: { fontFamily: "monospace" } }, msg.ToPhoneNumber || msg.toPhoneNumber), /* @__PURE__ */ import_react2.default.createElement("td", null, /* @__PURE__ */ import_react2.default.createElement("span", { className: `badge badge-${(msg.Status || msg.status || "unknown").toLowerCase()}` }, (msg.Status || msg.status) === "Sent" && /* @__PURE__ */ import_react2.default.createElement(import_lucide_react2.CheckCircle2, { size: 12, style: { marginRight: "4px", verticalAlign: "middle" } }), (msg.Status || msg.status) === "Failed" && /* @__PURE__ */ import_react2.default.createElement(import_lucide_react2.AlertCircle, { size: 12, style: { marginRight: "4px", verticalAlign: "middle" } }), (msg.Status || msg.status) === "Pending" && /* @__PURE__ */ import_react2.default.createElement(import_lucide_react2.Clock, { size: 12, style: { marginRight: "4px", verticalAlign: "middle" } }), msg.Status || msg.status)))))))), /* @__PURE__ */ import_react2.default.createElement("div", { className: "card", style: { flex: 1 } }, /* @__PURE__ */ import_react2.default.createElement("h3", { className: "card-title" }, "Recent Inbox Replies"), inbox.length === 0 ? /* @__PURE__ */ import_react2.default.createElement("div", { className: "empty-state" }, "No replies received yet.") : /* @__PURE__ */ import_react2.default.createElement("div", { className: "data-table-container" }, /* @__PURE__ */ import_react2.default.createElement("table", { className: "data-table" }, /* @__PURE__ */ import_react2.default.createElement("thead", null, /* @__PURE__ */ import_react2.default.createElement("tr", null, /* @__PURE__ */ import_react2.default.createElement("th", null, "From"), /* @__PURE__ */ import_react2.default.createElement("th", null, "Message"))), /* @__PURE__ */ import_react2.default.createElement("tbody", null, (inbox || []).map((msg) => /* @__PURE__ */ import_react2.default.createElement("tr", { key: msg.Id || msg.id }, /* @__PURE__ */ import_react2.default.createElement("td", { style: { fontFamily: "monospace" } }, msg.FromPhoneNumber || msg.fromPhoneNumber), /* @__PURE__ */ import_react2.default.createElement("td", null, msg.Content || msg.content)))))))));
    }
    if (activeTab === "contacts") {
      const handleFileSelect = (e) => {
        const file = e.target.files[0];
        if (!file) return;
        setIsImporting(true);
        import_papaparse.default.parse(file, {
          header: true,
          skipEmptyLines: true,
          complete: function(results) {
            const data = results.data;
            if (data.length === 0) {
              alert("CSV is empty");
              setIsImporting(false);
              return;
            }
            const headers = Object.keys(data[0]);
            setPreviewHeaders(headers);
            setPreviewData(data.slice(0, 100));
            const contactsList = data.map((row) => {
              let phone = null;
              let firstName = null;
              let lastName = null;
              let customFields = {};
              Object.keys(row).forEach((h) => {
                const hLower = h.toLowerCase();
                const val = row[h];
                if (!val || val.trim() === "") return;
                const cleanH = hLower.replace(/[^a-z0-9]/g, "");
                const phoneKeywords = ["phone", "phonenumber", "phoneno", "customerphonenumber", "ph", "phno", "telno", "mob", "mobilenumber", "mobile", "cellphone", "cell", "telephonenumber", "contactno", "contact", "contactnumber", "homephone", "workphone", "officephone", "landline", "fax", "tel", "telephone"];
                if (phoneKeywords.includes(cleanH)) phone = val;
                else if (cleanH === "firstname" || cleanH === "first_name") firstName = val;
                else if (cleanH === "lastname" || cleanH === "last_name") lastName = val;
                else customFields[h] = val;
              });
              return {
                PhoneNumber: phone,
                FirstName: firstName,
                LastName: lastName,
                CustomFieldsJson: JSON.stringify(customFields)
              };
            }).filter((c) => c.PhoneNumber);
            setParsedContacts(contactsList);
            setImportTotalRows(contactsList.length);
            setImportProgress(0);
            setIsImporting(false);
            setImportModalOpen(true);
            e.target.value = null;
          }
        });
      };
      const handleStartImport = async () => {
        setIsImporting(true);
        const chunkSize = 5e3;
        let imported = 0;
        for (let i = 0; i < parsedContacts.length; i += chunkSize) {
          const chunk = parsedContacts.slice(i, i + chunkSize).map((c) => ({
            ...c,
            CategoryId: selectedCategory || null
          }));
          try {
            const res = await fetch(`${API_BASE}/contacts/import/chunk`, {
              method: "POST",
              headers: { "Content-Type": "application/json" },
              body: JSON.stringify(chunk)
            });
            if (res.ok) {
              imported += chunk.length;
              setImportProgress(Math.min(imported, importTotalRows));
            } else {
              throw new Error("Failed chunk");
            }
          } catch (e) {
            alert("Import failed at chunk " + (Math.floor(i / chunkSize) + 1));
            break;
          }
        }
        alert(`Finished importing! Total imported: ${imported}`);
        setIsImporting(false);
        setImportModalOpen(false);
        setParsedContacts([]);
        fetchData();
      };
      const handleAddCategory = async () => {
        const name = prompt("Enter new category name:");
        if (!name) return;
        await fetch(`${API_BASE}/categories`, {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ name })
        });
        fetchData();
      };
      const handleDeleteAllContacts = async () => {
        if (!window.confirm("WARNING: Are you sure you want to delete ALL contacts? This cannot be undone!")) return;
        try {
          const res = await fetch(`${API_BASE}/contacts`, { method: "DELETE" });
          if (res.ok) {
            alert("All contacts deleted.");
            setCurrentPage(1);
            fetchData();
          }
        } catch (e) {
          alert("Error deleting contacts.");
        }
      };
      const handleDeleteContact = async (id) => {
        if (!window.confirm("Delete this contact?")) return;
        try {
          const res = await fetch(`${API_BASE}/contacts/${id}`, { method: "DELETE" });
          if (res.ok) fetchData();
        } catch (e) {
          alert("Error deleting contact.");
        }
      };
      const handleEditContactClick = (c) => {
        setEditingContactId(c.Id || c.id);
        setEditContactData({
          PhoneNumber: c.PhoneNumber || c.phoneNumber || "",
          FirstName: c.FirstName || c.firstName || "",
          LastName: c.LastName || c.lastName || "",
          CustomFieldsJson: c.CustomFieldsJson || c.customFieldsJson || "{}"
        });
        setShowEditContactModal(true);
      };
      const handleAddContactClick = () => {
        setEditingContactId(null);
        setEditContactData({
          PhoneNumber: "",
          FirstName: "",
          LastName: "",
          CustomFieldsJson: "{}"
        });
        setShowEditContactModal(true);
      };
      const handleSaveContact = async () => {
        try {
          const url = editingContactId ? `${API_BASE}/contacts/${editingContactId}` : `${API_BASE}/contacts`;
          const method = editingContactId ? "PUT" : "POST";
          const res = await fetch(url, {
            method,
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({
              ...editContactData,
              CategoryId: selectedCategory || null
            })
          });
          if (res.ok) {
            setShowEditContactModal(false);
            fetchData();
          } else {
            alert("Failed to save contact.");
          }
        } catch (e) {
          alert("Error saving contact.");
        }
      };
      const handleDeleteColumn = async (colName) => {
        if (!window.confirm(`Are you sure you want to permanently delete the column "${colName}" and all its data from ALL contacts?`)) return;
        try {
          const res = await fetch(`${API_BASE}/contacts/columns/${encodeURIComponent(colName)}`, { method: "DELETE" });
          if (res.ok) {
            alert(`Column ${colName} deleted successfully.`);
            fetchData();
          } else {
            alert("Failed to delete column.");
          }
        } catch (err) {
          alert("Error deleting column.");
        }
      };
      const handleDeleteCategory = async () => {
        if (!selectedCategory) return;
        if (!window.confirm("Are you sure you want to delete this category? Contacts inside it will NOT be deleted, but they will lose this category tag.")) return;
        try {
          const res = await fetch(`${API_BASE}/categories/${selectedCategory}`, { method: "DELETE" });
          if (res.ok) {
            setSelectedCategory("");
            fetchData();
          } else {
            alert("Failed to delete category.");
          }
        } catch (err) {
          alert("Error deleting category.");
        }
      };
      return /* @__PURE__ */ import_react2.default.createElement("div", null, /* @__PURE__ */ import_react2.default.createElement("div", { style: { display: "flex", gap: "16px", marginBottom: "24px", alignItems: "center" } }, /* @__PURE__ */ import_react2.default.createElement(
        "select",
        {
          value: selectedCategory,
          onChange: (e) => {
            setSelectedCategory(e.target.value);
            setTimeout(fetchData, 100);
          },
          style: { padding: "8px", borderRadius: "4px", border: "1px solid #ccc" }
        },
        /* @__PURE__ */ import_react2.default.createElement("option", { value: "" }, "All Categories"),
        (categories || []).map((c) => /* @__PURE__ */ import_react2.default.createElement("option", { key: c.Id || c.id, value: c.Id || c.id }, c.Name || c.name))
      ), /* @__PURE__ */ import_react2.default.createElement("button", { className: "btn btn-secondary", onClick: handleAddCategory }, /* @__PURE__ */ import_react2.default.createElement(import_lucide_react2.Plus, { size: 16 }), " New Category"), selectedCategory && /* @__PURE__ */ import_react2.default.createElement("button", { className: "btn btn-secondary", style: { color: "#ef4444" }, onClick: handleDeleteCategory }, /* @__PURE__ */ import_react2.default.createElement(import_lucide_react2.Trash2, { size: 16 }), " Delete Category"), /* @__PURE__ */ import_react2.default.createElement("div", { style: { flex: 1 } }), /* @__PURE__ */ import_react2.default.createElement("button", { className: "btn btn-secondary", onClick: () => setShowColumnModal(true) }, /* @__PURE__ */ import_react2.default.createElement(import_lucide_react2.Settings, { size: 16 }), " Manage Columns"), /* @__PURE__ */ import_react2.default.createElement("button", { className: "btn btn-secondary", onClick: handleAddContactClick }, /* @__PURE__ */ import_react2.default.createElement(import_lucide_react2.Plus, { size: 16 }), " Add Contact"), /* @__PURE__ */ import_react2.default.createElement("button", { className: "btn btn-secondary", onClick: handleDeleteAllContacts, style: { color: "#ef4444", borderColor: "#ef4444" } }, /* @__PURE__ */ import_react2.default.createElement(import_lucide_react2.Trash2, { size: 16 }), " Delete All"), /* @__PURE__ */ import_react2.default.createElement("label", { className: "btn btn-primary", style: { cursor: isImporting ? "not-allowed" : "pointer", backgroundColor: isImporting ? "#94a3b8" : "#10b981" } }, isImporting ? "Loading CSV..." : /* @__PURE__ */ import_react2.default.createElement(import_react2.default.Fragment, null, /* @__PURE__ */ import_react2.default.createElement(import_lucide_react2.Upload, { size: 16 }), " Import CSV"), /* @__PURE__ */ import_react2.default.createElement("input", { type: "file", accept: ".csv", style: { display: "none" }, onChange: handleFileSelect, disabled: isImporting }))), showColumnModal && /* @__PURE__ */ import_react2.default.createElement("div", { style: { position: "fixed", top: 0, left: 0, right: 0, bottom: 0, backgroundColor: "rgba(0,0,0,0.5)", display: "flex", justifyContent: "center", alignItems: "center", zIndex: 1e3 } }, /* @__PURE__ */ import_react2.default.createElement("div", { className: "card", style: { width: "400px" } }, /* @__PURE__ */ import_react2.default.createElement("h3", { className: "card-title", style: { marginBottom: "16px" } }, "Manage Custom Columns"), /* @__PURE__ */ import_react2.default.createElement("p", { style: { fontSize: "0.9em", color: "#64748b", marginBottom: "16px" } }, "These columns were generated dynamically from your CSV imports. Deleting a column here will remove it from all contacts permanently."), (dynamicHeaders || []).length === 0 ? /* @__PURE__ */ import_react2.default.createElement("div", { className: "empty-state" }, "No custom columns found.") : /* @__PURE__ */ import_react2.default.createElement("div", { style: { display: "flex", flexDirection: "column", gap: "8px", maxHeight: "300px", overflowY: "auto" } }, (dynamicHeaders || []).map((h) => /* @__PURE__ */ import_react2.default.createElement("div", { key: h, style: { display: "flex", justifyContent: "space-between", alignItems: "center", padding: "8px", border: "1px solid #e2e8f0", borderRadius: "4px" } }, /* @__PURE__ */ import_react2.default.createElement("span", { style: { fontWeight: 500 } }, h), /* @__PURE__ */ import_react2.default.createElement("button", { className: "btn btn-secondary", style: { padding: "4px 8px", color: "#ef4444" }, onClick: () => handleDeleteColumn(h) }, /* @__PURE__ */ import_react2.default.createElement(import_lucide_react2.Trash2, { size: 14 }))))), /* @__PURE__ */ import_react2.default.createElement("div", { style: { display: "flex", justifyContent: "flex-end", marginTop: "16px" } }, /* @__PURE__ */ import_react2.default.createElement("button", { className: "btn btn-primary", onClick: () => setShowColumnModal(false) }, "Close")))), /* @__PURE__ */ import_react2.default.createElement("div", { className: "card" }, /* @__PURE__ */ import_react2.default.createElement("h3", { className: "card-title" }, "Contacts Directory"), contacts.length === 0 ? /* @__PURE__ */ import_react2.default.createElement("div", { className: "empty-state" }, "No contacts found. Try importing a CSV!") : /* @__PURE__ */ import_react2.default.createElement("div", { className: "data-table-container" }, /* @__PURE__ */ import_react2.default.createElement("table", { className: "data-table" }, /* @__PURE__ */ import_react2.default.createElement("thead", null, /* @__PURE__ */ import_react2.default.createElement("tr", null, /* @__PURE__ */ import_react2.default.createElement("th", null, "Phone"), /* @__PURE__ */ import_react2.default.createElement("th", null, "First Name"), /* @__PURE__ */ import_react2.default.createElement("th", null, "Last Name"), (dynamicHeaders || []).map((h) => /* @__PURE__ */ import_react2.default.createElement("th", { key: h }, h)), /* @__PURE__ */ import_react2.default.createElement("th", null, "Actions"))), /* @__PURE__ */ import_react2.default.createElement("tbody", null, contacts.map((c) => {
        let fields = {};
        try {
          fields = JSON.parse(c.CustomFieldsJson || c.customFieldsJson || "{}");
        } catch (e) {
        }
        return /* @__PURE__ */ import_react2.default.createElement("tr", { key: c.Id || c.id }, /* @__PURE__ */ import_react2.default.createElement("td", { style: { fontFamily: "monospace" } }, c.PhoneNumber || c.phoneNumber), /* @__PURE__ */ import_react2.default.createElement("td", null, c.FirstName || c.firstName), /* @__PURE__ */ import_react2.default.createElement("td", null, c.LastName || c.lastName), (dynamicHeaders || []).map((h) => /* @__PURE__ */ import_react2.default.createElement("td", { key: h }, formatDateValue(fields[h]))), /* @__PURE__ */ import_react2.default.createElement("td", null, /* @__PURE__ */ import_react2.default.createElement("div", { style: { display: "flex", gap: "8px" } }, /* @__PURE__ */ import_react2.default.createElement("button", { className: "btn btn-secondary", style: { padding: "4px", border: "none" }, onClick: () => handleEditContactClick(c) }, /* @__PURE__ */ import_react2.default.createElement(import_lucide_react2.Edit2, { size: 16, color: "#3b82f6" })), /* @__PURE__ */ import_react2.default.createElement("button", { className: "btn btn-secondary", style: { padding: "4px", border: "none" }, onClick: () => handleDeleteContact(c.Id || c.id) }, /* @__PURE__ */ import_react2.default.createElement(import_lucide_react2.Trash2, { size: 16, color: "#ef4444" })))));
      })))), contacts.length > 0 && /* @__PURE__ */ import_react2.default.createElement("div", { style: { display: "flex", justifyContent: "space-between", alignItems: "center", marginTop: "16px", padding: "16px", borderTop: "1px solid #e2e8f0" } }, /* @__PURE__ */ import_react2.default.createElement("div", { style: { display: "flex", alignItems: "center", gap: "8px" } }, /* @__PURE__ */ import_react2.default.createElement("span", { style: { color: "#64748b" } }, "Rows per page:"), /* @__PURE__ */ import_react2.default.createElement(
        "select",
        {
          value: pageSize,
          onChange: (e) => {
            setPageSize(Number(e.target.value));
            setCurrentPage(1);
          },
          style: { padding: "4px 8px", borderRadius: "4px", border: "1px solid #ccc" }
        },
        /* @__PURE__ */ import_react2.default.createElement("option", { value: 10 }, "10"),
        /* @__PURE__ */ import_react2.default.createElement("option", { value: 20 }, "20"),
        /* @__PURE__ */ import_react2.default.createElement("option", { value: 50 }, "50"),
        /* @__PURE__ */ import_react2.default.createElement("option", { value: 100 }, "100")
      ), /* @__PURE__ */ import_react2.default.createElement("span", { style: { color: "#64748b", marginLeft: "16px" } }, "Total: ", totalContacts)), /* @__PURE__ */ import_react2.default.createElement("div", { style: { display: "flex", alignItems: "center", gap: "16px" } }, /* @__PURE__ */ import_react2.default.createElement(
        "button",
        {
          className: "btn btn-secondary",
          disabled: currentPage === 1,
          onClick: () => setCurrentPage((p) => Math.max(1, p - 1))
        },
        "Previous"
      ), /* @__PURE__ */ import_react2.default.createElement("span", { style: { fontWeight: 500 } }, "Page ", currentPage, " of ", Math.ceil(totalContacts / pageSize) || 1), /* @__PURE__ */ import_react2.default.createElement(
        "button",
        {
          className: "btn btn-secondary",
          disabled: currentPage >= Math.ceil(totalContacts / pageSize),
          onClick: () => setCurrentPage((p) => p + 1)
        },
        "Next"
      )))), showEditContactModal && /* @__PURE__ */ import_react2.default.createElement("div", { style: { position: "fixed", top: 0, left: 0, right: 0, bottom: 0, backgroundColor: "rgba(0,0,0,0.5)", display: "flex", justifyContent: "center", alignItems: "center", zIndex: 1e3 } }, /* @__PURE__ */ import_react2.default.createElement("div", { className: "card", style: { width: "500px" } }, /* @__PURE__ */ import_react2.default.createElement("h3", { className: "card-title", style: { marginBottom: "16px" } }, editingContactId ? "Edit Contact" : "Add Contact"), /* @__PURE__ */ import_react2.default.createElement("div", { className: "form-group", style: { marginBottom: "16px" } }, /* @__PURE__ */ import_react2.default.createElement("label", { style: { display: "block", marginBottom: "8px", fontWeight: 500 } }, "Phone Number"), /* @__PURE__ */ import_react2.default.createElement("input", { type: "text", value: editContactData.PhoneNumber, onChange: (e) => setEditContactData({ ...editContactData, PhoneNumber: e.target.value }), style: { width: "100%", padding: "8px", borderRadius: "4px", border: "1px solid #ccc" } })), /* @__PURE__ */ import_react2.default.createElement("div", { style: { display: "flex", gap: "16px", marginBottom: "16px" } }, /* @__PURE__ */ import_react2.default.createElement("div", { className: "form-group", style: { flex: 1 } }, /* @__PURE__ */ import_react2.default.createElement("label", { style: { display: "block", marginBottom: "8px", fontWeight: 500 } }, "First Name"), /* @__PURE__ */ import_react2.default.createElement("input", { type: "text", value: editContactData.FirstName, onChange: (e) => setEditContactData({ ...editContactData, FirstName: e.target.value }), style: { width: "100%", padding: "8px", borderRadius: "4px", border: "1px solid #ccc" } })), /* @__PURE__ */ import_react2.default.createElement("div", { className: "form-group", style: { flex: 1 } }, /* @__PURE__ */ import_react2.default.createElement("label", { style: { display: "block", marginBottom: "8px", fontWeight: 500 } }, "Last Name"), /* @__PURE__ */ import_react2.default.createElement("input", { type: "text", value: editContactData.LastName, onChange: (e) => setEditContactData({ ...editContactData, LastName: e.target.value }), style: { width: "100%", padding: "8px", borderRadius: "4px", border: "1px solid #ccc" } }))), /* @__PURE__ */ import_react2.default.createElement("div", { style: { marginBottom: "16px" } }, /* @__PURE__ */ import_react2.default.createElement("label", { style: { display: "block", marginBottom: "16px", fontWeight: 600, borderBottom: "1px solid #e2e8f0", paddingBottom: "8px" } }, "Custom Fields"), (dynamicHeaders || []).length === 0 ? /* @__PURE__ */ import_react2.default.createElement("p", { style: { color: "#64748b", fontSize: "0.9em" } }, "No custom columns found.") : /* @__PURE__ */ import_react2.default.createElement("div", { style: { display: "grid", gridTemplateColumns: "1fr 1fr", gap: "16px" } }, (dynamicHeaders || []).map((header) => {
        let fields = {};
        try {
          fields = JSON.parse(editContactData.CustomFieldsJson || "{}");
        } catch (e) {
        }
        return /* @__PURE__ */ import_react2.default.createElement("div", { key: header, className: "form-group" }, /* @__PURE__ */ import_react2.default.createElement("label", { style: { display: "block", marginBottom: "4px", fontWeight: 500, fontSize: "0.9em", color: "#475569" } }, header), /* @__PURE__ */ import_react2.default.createElement(
          "input",
          {
            type: "text",
            value: fields[header] || "",
            onChange: (e) => {
              let newFields = { ...fields };
              newFields[header] = e.target.value;
              setEditContactData({ ...editContactData, CustomFieldsJson: JSON.stringify(newFields) });
            },
            style: { width: "100%", padding: "6px 8px", borderRadius: "4px", border: "1px solid #ccc" }
          }
        ));
      }))), /* @__PURE__ */ import_react2.default.createElement("div", { style: { display: "flex", justifyContent: "flex-end", gap: "12px" } }, /* @__PURE__ */ import_react2.default.createElement("button", { className: "btn btn-secondary", onClick: () => setShowEditContactModal(false) }, "Cancel"), /* @__PURE__ */ import_react2.default.createElement("button", { className: "btn btn-primary", onClick: handleSaveContact }, "Save")))), importModalOpen && /* @__PURE__ */ import_react2.default.createElement("div", { style: { position: "fixed", top: 0, left: 0, right: 0, bottom: 0, backgroundColor: "rgba(0,0,0,0.5)", display: "flex", justifyContent: "center", alignItems: "center", zIndex: 1e3 } }, /* @__PURE__ */ import_react2.default.createElement("div", { className: "card", style: { width: "800px", maxHeight: "90vh", overflowY: "auto" } }, /* @__PURE__ */ import_react2.default.createElement("h3", { className: "card-title", style: { marginBottom: "16px" } }, "Import Contacts Preview"), /* @__PURE__ */ import_react2.default.createElement("div", { style: { display: "flex", gap: "16px", marginBottom: "24px", alignItems: "center" } }, /* @__PURE__ */ import_react2.default.createElement("div", { style: { flex: 1 } }, /* @__PURE__ */ import_react2.default.createElement("label", { style: { display: "block", marginBottom: "8px", fontWeight: 500 } }, "Select Category for these contacts"), /* @__PURE__ */ import_react2.default.createElement("div", { style: { display: "flex", gap: "8px" } }, /* @__PURE__ */ import_react2.default.createElement(
        "select",
        {
          value: selectedCategory,
          onChange: (e) => setSelectedCategory(e.target.value),
          style: { padding: "8px", borderRadius: "4px", border: "1px solid #ccc", flex: 1 }
        },
        /* @__PURE__ */ import_react2.default.createElement("option", { value: "" }, "No Category (All Contacts)"),
        (categories || []).map((c) => /* @__PURE__ */ import_react2.default.createElement("option", { key: c.Id || c.id, value: c.Id || c.id }, c.Name || c.name))
      ), /* @__PURE__ */ import_react2.default.createElement("button", { className: "btn btn-secondary", onClick: handleAddCategory }, /* @__PURE__ */ import_react2.default.createElement(import_lucide_react2.Plus, { size: 16 }), " Create New")))), /* @__PURE__ */ import_react2.default.createElement("div", { style: { marginBottom: "16px" } }, /* @__PURE__ */ import_react2.default.createElement("p", { style: { fontWeight: 500 } }, "Total Valid Contacts Detected: ", importTotalRows), /* @__PURE__ */ import_react2.default.createElement("p", { style: { color: "#64748b", fontSize: "0.9em" } }, "Previewing first 100 rows:")), /* @__PURE__ */ import_react2.default.createElement("div", { className: "data-table-container", style: { maxHeight: "300px", marginBottom: "24px" } }, /* @__PURE__ */ import_react2.default.createElement("table", { className: "data-table" }, /* @__PURE__ */ import_react2.default.createElement("thead", null, /* @__PURE__ */ import_react2.default.createElement("tr", null, previewHeaders.map((h) => /* @__PURE__ */ import_react2.default.createElement("th", { key: h }, h)))), /* @__PURE__ */ import_react2.default.createElement("tbody", null, previewData.map((row, i) => /* @__PURE__ */ import_react2.default.createElement("tr", { key: i }, previewHeaders.map((h) => /* @__PURE__ */ import_react2.default.createElement("td", { key: h }, row[h]))))))), isImporting && /* @__PURE__ */ import_react2.default.createElement("div", { style: { marginBottom: "24px" } }, /* @__PURE__ */ import_react2.default.createElement("div", { style: { display: "flex", justifyContent: "space-between", marginBottom: "8px" } }, /* @__PURE__ */ import_react2.default.createElement("span", { style: { fontWeight: 500 } }, "Importing Contacts..."), /* @__PURE__ */ import_react2.default.createElement("span", null, importProgress, " / ", importTotalRows)), /* @__PURE__ */ import_react2.default.createElement("div", { style: { width: "100%", height: "8px", backgroundColor: "#e2e8f0", borderRadius: "4px", overflow: "hidden" } }, /* @__PURE__ */ import_react2.default.createElement("div", { style: { width: `${importProgress / importTotalRows * 100}%`, height: "100%", backgroundColor: "#10b981", transition: "width 0.3s ease" } }))), /* @__PURE__ */ import_react2.default.createElement("div", { style: { display: "flex", justifyContent: "flex-end", gap: "12px" } }, /* @__PURE__ */ import_react2.default.createElement("button", { className: "btn btn-secondary", onClick: () => setImportModalOpen(false), disabled: isImporting }, "Cancel"), /* @__PURE__ */ import_react2.default.createElement("button", { className: "btn btn-primary", onClick: handleStartImport, disabled: isImporting || importTotalRows === 0 }, isImporting ? "Processing..." : "Start Import")))));
    }
    if (activeTab === "templates") {
      const handleSaveTemplate = async () => {
        if (!newTemplate.Name || !newTemplate.ExternalTemplateName) {
          alert("Name and External Template Name are required");
          return;
        }
        try {
          const url = isEditingTemplate ? `${API_BASE}/templates/${editingTemplateId}` : `${API_BASE}/templates`;
          const method = isEditingTemplate ? "PUT" : "POST";
          const res = await fetch(url, {
            method,
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(newTemplate)
          });
          if (res.ok) {
            setShowTemplateModal(false);
            setNewTemplate({ Name: "", ExternalTemplateName: "", LanguageCode: "en", ChannelType: "WhatsApp" });
            setIsEditingTemplate(false);
            setEditingTemplateId(null);
            fetchData();
          } else {
            alert("Failed to save template");
          }
        } catch (err) {
          alert("Error saving template");
        }
      };
      const handleEditTemplate = (t) => {
        setNewTemplate({
          Name: t.Name || t.name,
          ExternalTemplateName: t.ExternalTemplateName || t.externalTemplateName || "",
          LanguageCode: t.LanguageCode || t.languageCode || "en",
          ChannelType: t.ChannelType || t.channelType || "WhatsApp"
        });
        setEditingTemplateId(t.Id || t.id);
        setIsEditingTemplate(true);
        setShowTemplateModal(true);
      };
      const handleDeleteTemplate = async (id) => {
        if (!window.confirm("Are you sure you want to delete this template?")) return;
        try {
          const res = await fetch(`${API_BASE}/templates/${id}`, { method: "DELETE" });
          if (res.ok) {
            fetchData();
          } else {
            alert("Failed to delete template");
          }
        } catch (err) {
          alert("Error deleting template");
        }
      };
      return /* @__PURE__ */ import_react2.default.createElement("div", { className: "card" }, /* @__PURE__ */ import_react2.default.createElement("div", { style: { display: "flex", justifyContent: "space-between", marginBottom: "16px" } }, /* @__PURE__ */ import_react2.default.createElement("h3", { className: "card-title" }, "Message Templates"), /* @__PURE__ */ import_react2.default.createElement("button", { className: "btn btn-primary", onClick: () => {
        setNewTemplate({ Name: "", ExternalTemplateName: "", LanguageCode: "en", ChannelType: "WhatsApp" });
        setIsEditingTemplate(false);
        setEditingTemplateId(null);
        setShowTemplateModal(true);
      } }, /* @__PURE__ */ import_react2.default.createElement(import_lucide_react2.Plus, { size: 16 }), " Create Template")), showTemplateModal && /* @__PURE__ */ import_react2.default.createElement("div", { style: { position: "fixed", top: 0, left: 0, right: 0, bottom: 0, backgroundColor: "rgba(0,0,0,0.5)", display: "flex", justifyContent: "center", alignItems: "center", zIndex: 1e3 } }, /* @__PURE__ */ import_react2.default.createElement("div", { className: "card", style: { width: "400px" } }, /* @__PURE__ */ import_react2.default.createElement("h3", { className: "card-title", style: { marginBottom: "16px" } }, isEditingTemplate ? "Edit Template" : "New Template"), /* @__PURE__ */ import_react2.default.createElement("div", { style: { display: "flex", flexDirection: "column", gap: "12px" } }, /* @__PURE__ */ import_react2.default.createElement("input", { type: "text", className: "input", placeholder: "Display Name (e.g. CheckIn Welcome)", value: newTemplate.Name, onChange: (e) => setNewTemplate({ ...newTemplate, Name: e.target.value }), style: { padding: "8px", borderRadius: "4px", border: "1px solid #ccc" } }), /* @__PURE__ */ import_react2.default.createElement("input", { type: "text", className: "input", placeholder: "Meta Template Name (e.g. hotel_checkin)", value: newTemplate.ExternalTemplateName, onChange: (e) => setNewTemplate({ ...newTemplate, ExternalTemplateName: e.target.value }), style: { padding: "8px", borderRadius: "4px", border: "1px solid #ccc" } }), /* @__PURE__ */ import_react2.default.createElement("input", { type: "text", className: "input", placeholder: "Language (e.g. en)", value: newTemplate.LanguageCode, onChange: (e) => setNewTemplate({ ...newTemplate, LanguageCode: e.target.value }), style: { padding: "8px", borderRadius: "4px", border: "1px solid #ccc" } }), /* @__PURE__ */ import_react2.default.createElement("select", { className: "input", value: newTemplate.ChannelType, onChange: (e) => setNewTemplate({ ...newTemplate, ChannelType: e.target.value }), style: { padding: "8px", borderRadius: "4px", border: "1px solid #ccc" } }, /* @__PURE__ */ import_react2.default.createElement("option", { value: "WhatsApp" }, "WhatsApp"), /* @__PURE__ */ import_react2.default.createElement("option", { value: "SMS" }, "SMS")), /* @__PURE__ */ import_react2.default.createElement("div", { style: { display: "flex", justifyContent: "flex-end", gap: "8px", marginTop: "16px" } }, /* @__PURE__ */ import_react2.default.createElement("button", { className: "btn btn-secondary", onClick: () => setShowTemplateModal(false) }, "Cancel"), /* @__PURE__ */ import_react2.default.createElement("button", { className: "btn btn-primary", onClick: handleSaveTemplate }, "Save"))))), /* @__PURE__ */ import_react2.default.createElement("div", { className: "data-table-container" }, /* @__PURE__ */ import_react2.default.createElement("table", { className: "data-table" }, /* @__PURE__ */ import_react2.default.createElement("thead", null, /* @__PURE__ */ import_react2.default.createElement("tr", null, /* @__PURE__ */ import_react2.default.createElement("th", null, "Name"), /* @__PURE__ */ import_react2.default.createElement("th", null, "External Name"), /* @__PURE__ */ import_react2.default.createElement("th", null, "Channel"), /* @__PURE__ */ import_react2.default.createElement("th", null, "Actions"))), /* @__PURE__ */ import_react2.default.createElement("tbody", null, (templatesList || []).map((t) => /* @__PURE__ */ import_react2.default.createElement("tr", { key: t.Id || t.id }, /* @__PURE__ */ import_react2.default.createElement("td", null, t.Name || t.name), /* @__PURE__ */ import_react2.default.createElement("td", null, t.ExternalTemplateName || t.externalTemplateName || "-"), /* @__PURE__ */ import_react2.default.createElement("td", null, /* @__PURE__ */ import_react2.default.createElement("span", { className: "badge badge-sent" }, t.ChannelType || t.channelType)), /* @__PURE__ */ import_react2.default.createElement("td", null, /* @__PURE__ */ import_react2.default.createElement("div", { style: { display: "flex", gap: "8px" } }, /* @__PURE__ */ import_react2.default.createElement("button", { className: "btn btn-secondary", style: { padding: "4px 8px" }, onClick: () => handleEditTemplate(t) }, /* @__PURE__ */ import_react2.default.createElement(import_lucide_react2.Edit2, { size: 14 })), /* @__PURE__ */ import_react2.default.createElement("button", { className: "btn btn-secondary", style: { padding: "4px 8px", color: "#ef4444" }, onClick: () => handleDeleteTemplate(t.Id || t.id) }, /* @__PURE__ */ import_react2.default.createElement(import_lucide_react2.Trash2, { size: 14 }))))))))));
    }
    if (activeTab === "rules") {
      const handleDeleteRule = async (id) => {
        if (!window.confirm("Are you sure you want to delete this automation/workflow?")) return;
        try {
          const res = await fetch(`${API_BASE}/rules/${id}`, { method: "DELETE" });
          if (res.ok) {
            fetchData();
          } else {
            alert("Failed to delete rule");
          }
        } catch (err) {
          alert("Error deleting rule");
        }
      };
      const handleToggleRule = async (r) => {
        try {
          const res = await fetch(`${API_BASE}/rules/${r.Id || r.id}/toggle`, { method: "PUT" });
          if (res.ok) {
            fetchData();
          } else {
            alert("Failed to toggle rule");
          }
        } catch (err) {
          alert("Error toggling rule");
        }
      };
      const [statsModalRule, setStatsModalRule] = (0, import_react2.useState)(null);
      const [ruleStats, setRuleStats] = (0, import_react2.useState)(null);
      const handleViewStats = async (r) => {
        setStatsModalRule(r);
        setRuleStats(null);
        try {
          const res = await fetch(`${API_BASE}/rules/${r.Id || r.id}/stats`);
          if (res.ok) {
            setRuleStats(await res.json());
          }
        } catch (err) {
        }
      };
      if (isBuildingRule) {
        return /* @__PURE__ */ import_react2.default.createElement(AutomationBuilder, { initialRule: editingRule, onCancel: () => {
          setIsBuildingRule(false);
          setEditingRule(null);
        } });
      }
      return /* @__PURE__ */ import_react2.default.createElement("div", { style: { padding: "0 12px" } }, /* @__PURE__ */ import_react2.default.createElement("div", { style: { display: "flex", justifyContent: "space-between", marginBottom: "24px", alignItems: "center" } }, /* @__PURE__ */ import_react2.default.createElement("h3", { style: { fontSize: "1.25rem", fontWeight: 700, margin: 0, display: "flex", alignItems: "center", gap: "8px" } }, /* @__PURE__ */ import_react2.default.createElement(import_lucide_react2.Workflow, { size: 24, color: "#6366f1" }), " Automations"), /* @__PURE__ */ import_react2.default.createElement("button", { className: "btn btn-primary", onClick: () => {
        setEditingRule(null);
        setIsBuildingRule(true);
      }, style: { display: "flex", gap: "6px", alignItems: "center", background: "#6366f1" } }, /* @__PURE__ */ import_react2.default.createElement(import_lucide_react2.Plus, { size: 16 }), " Create Automation")), /* @__PURE__ */ import_react2.default.createElement("div", { style: { display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(320px, 1fr))", gap: "20px" } }, (rulesList || []).map((r) => /* @__PURE__ */ import_react2.default.createElement("div", { key: r.Id || r.id, style: {
        background: "#fff",
        borderRadius: "12px",
        border: "1px solid #e2e8f0",
        boxShadow: "0 4px 6px -1px rgba(0,0,0,0.05)",
        display: "flex",
        flexDirection: "column",
        overflow: "hidden"
      } }, /* @__PURE__ */ import_react2.default.createElement("div", { style: { padding: "16px", borderBottom: "1px solid #f1f5f9", display: "flex", justifyContent: "space-between", alignItems: "flex-start" } }, /* @__PURE__ */ import_react2.default.createElement("div", null, /* @__PURE__ */ import_react2.default.createElement("h4", { style: { margin: "0 0 4px 0", fontSize: "1.05rem", fontWeight: 600, color: "#0f172a" } }, r.Name || `Workflow ${r.Id || r.id}`), /* @__PURE__ */ import_react2.default.createElement("p", { style: { margin: 0, fontSize: "0.8rem", color: "#64748b" } }, r.Description || "No description provided")), /* @__PURE__ */ import_react2.default.createElement("div", { style: { display: "flex", alignItems: "center" } }, /* @__PURE__ */ import_react2.default.createElement("label", { style: { display: "flex", alignItems: "center", cursor: "pointer" } }, /* @__PURE__ */ import_react2.default.createElement("input", { type: "checkbox", checked: r.IsEnabled !== false && r.isEnabled !== false, onChange: () => handleToggleRule(r), style: {
        appearance: "none",
        width: "36px",
        height: "20px",
        background: r.IsEnabled !== false && r.isEnabled !== false ? "#10b981" : "#cbd5e1",
        borderRadius: "20px",
        position: "relative",
        outline: "none",
        transition: "background 0.2s",
        cursor: "pointer"
      } }), /* @__PURE__ */ import_react2.default.createElement("span", { style: {
        position: "absolute",
        width: "16px",
        height: "16px",
        background: "#fff",
        borderRadius: "50%",
        transform: r.IsEnabled !== false && r.isEnabled !== false ? "translateX(18px)" : "translateX(2px)",
        transition: "transform 0.2s",
        pointerEvents: "none"
      } })))), /* @__PURE__ */ import_react2.default.createElement("div", { style: { padding: "16px", flex: 1 } }, /* @__PURE__ */ import_react2.default.createElement("div", { style: { display: "flex", alignItems: "center", gap: "12px", marginBottom: "12px" } }, /* @__PURE__ */ import_react2.default.createElement("div", { style: { background: "#eff6ff", padding: "6px", borderRadius: "8px" } }, /* @__PURE__ */ import_react2.default.createElement(import_lucide_react2.Zap, { size: 16, color: "#3b82f6" })), /* @__PURE__ */ import_react2.default.createElement("div", null, /* @__PURE__ */ import_react2.default.createElement("div", { style: { fontSize: "0.7rem", textTransform: "uppercase", color: "#94a3b8", fontWeight: 600, letterSpacing: "0.05em" } }, "Trigger"), /* @__PURE__ */ import_react2.default.createElement("div", { style: { fontSize: "0.85rem", fontWeight: 500 } }, r.TriggerEventType === "DateTrigger" ? "Date Based" : r.TriggerEventType || "Manual"))), /* @__PURE__ */ import_react2.default.createElement("div", { style: { display: "flex", alignItems: "center", gap: "12px" } }, /* @__PURE__ */ import_react2.default.createElement("div", { style: { background: "#f8fafc", padding: "6px", borderRadius: "8px" } }, /* @__PURE__ */ import_react2.default.createElement(import_lucide_react2.Users, { size: 16, color: "#64748b" })), /* @__PURE__ */ import_react2.default.createElement("div", null, /* @__PURE__ */ import_react2.default.createElement("div", { style: { fontSize: "0.7rem", textTransform: "uppercase", color: "#94a3b8", fontWeight: 600, letterSpacing: "0.05em" } }, "Send To"), /* @__PURE__ */ import_react2.default.createElement("div", { style: { fontSize: "0.85rem", fontWeight: 500 } }, r.AudienceFilter ? "Specific Audience" : "All Contacts")))), /* @__PURE__ */ import_react2.default.createElement("div", { style: { padding: "12px 16px", background: "#f8fafc", display: "flex", justifyContent: "space-between", alignItems: "center", borderTop: "1px solid #f1f5f9" } }, /* @__PURE__ */ import_react2.default.createElement("button", { onClick: () => handleViewStats(r), style: { background: "none", border: "none", color: "#6366f1", fontSize: "0.8rem", fontWeight: 500, cursor: "pointer", display: "flex", alignItems: "center", gap: "4px" } }, "View Runs"), /* @__PURE__ */ import_react2.default.createElement("div", { style: { display: "flex", gap: "8px" } }, /* @__PURE__ */ import_react2.default.createElement("button", { className: "btn btn-secondary", style: { padding: "6px" }, onClick: () => handleEditRule(r) }, /* @__PURE__ */ import_react2.default.createElement(import_lucide_react2.Edit2, { size: 16 })), /* @__PURE__ */ import_react2.default.createElement("button", { className: "btn btn-secondary", style: { padding: "6px", color: "#ef4444" }, onClick: () => handleDeleteRule(r.Id || r.id) }, /* @__PURE__ */ import_react2.default.createElement(import_lucide_react2.Trash2, { size: 16 }))))))), statsModalRule && /* @__PURE__ */ import_react2.default.createElement("div", { style: { position: "fixed", inset: 0, zIndex: 1e3, display: "flex", alignItems: "center", justifyContent: "center" } }, /* @__PURE__ */ import_react2.default.createElement("div", { style: { position: "absolute", inset: 0, background: "rgba(15, 23, 42, 0.4)", backdropFilter: "blur(4px)" }, onClick: () => setStatsModalRule(null) }), /* @__PURE__ */ import_react2.default.createElement("div", { style: { background: "#fff", borderRadius: "16px", padding: "24px", width: "90%", maxWidth: "400px", position: "relative", boxShadow: "0 20px 25px -5px rgba(0, 0, 0, 0.1)" } }, /* @__PURE__ */ import_react2.default.createElement("div", { style: { display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "24px" } }, /* @__PURE__ */ import_react2.default.createElement("h3", { style: { margin: 0, fontSize: "1.2rem", fontWeight: 700 } }, statsModalRule.Name || "Analytics"), /* @__PURE__ */ import_react2.default.createElement("button", { onClick: () => setStatsModalRule(null), style: { background: "none", border: "none", cursor: "pointer", color: "#64748b" } }, /* @__PURE__ */ import_react2.default.createElement(import_lucide_react2.XCircle, { size: 20 }))), ruleStats ? /* @__PURE__ */ import_react2.default.createElement("div", { style: { display: "flex", flexDirection: "column", gap: "16px" } }, /* @__PURE__ */ import_react2.default.createElement("div", { style: { display: "flex", justifyContent: "space-between", padding: "12px", background: "#f8fafc", borderRadius: "8px" } }, /* @__PURE__ */ import_react2.default.createElement("div", { style: { fontSize: "0.85rem", color: "#64748b" } }, "Total Sent"), /* @__PURE__ */ import_react2.default.createElement("div", { style: { fontSize: "1.25rem", fontWeight: 700, color: "#10b981" } }, ruleStats.TotalSent)), /* @__PURE__ */ import_react2.default.createElement("div", { style: { display: "flex", justifyContent: "space-between", padding: "12px", background: "#f8fafc", borderRadius: "8px" } }, /* @__PURE__ */ import_react2.default.createElement("div", { style: { fontSize: "0.85rem", color: "#64748b" } }, "Failed"), /* @__PURE__ */ import_react2.default.createElement("div", { style: { fontSize: "1.25rem", fontWeight: 700, color: "#ef4444" } }, ruleStats.Failed)), /* @__PURE__ */ import_react2.default.createElement("div", { style: { display: "flex", justifyContent: "space-between", padding: "12px", background: "#f8fafc", borderRadius: "8px" } }, /* @__PURE__ */ import_react2.default.createElement("div", { style: { fontSize: "0.85rem", color: "#64748b" } }, "Last Run"), /* @__PURE__ */ import_react2.default.createElement("div", { style: { fontSize: "0.85rem", fontWeight: 600 } }, new Date(ruleStats.LastRun).toLocaleString()))) : /* @__PURE__ */ import_react2.default.createElement("div", { style: { padding: "24px", textAlign: "center", color: "#64748b" } }, "Loading stats..."))));
    }
    if (activeTab === "logs") {
      return /* @__PURE__ */ import_react2.default.createElement("div", { className: "card" }, /* @__PURE__ */ import_react2.default.createElement("h3", { className: "card-title" }, "Event Logs"), /* @__PURE__ */ import_react2.default.createElement("div", { className: "data-table-container" }, /* @__PURE__ */ import_react2.default.createElement("table", { className: "data-table" }, /* @__PURE__ */ import_react2.default.createElement("thead", null, /* @__PURE__ */ import_react2.default.createElement("tr", null, /* @__PURE__ */ import_react2.default.createElement("th", null, "Occurred At"), /* @__PURE__ */ import_react2.default.createElement("th", null, "Event Type"), /* @__PURE__ */ import_react2.default.createElement("th", null, "Payload"))), /* @__PURE__ */ import_react2.default.createElement("tbody", null, (logs || []).map((l) => /* @__PURE__ */ import_react2.default.createElement("tr", { key: l.Id || l.id }, /* @__PURE__ */ import_react2.default.createElement("td", { style: { whiteSpace: "nowrap" } }, new Date(l.OccurredAt || l.occurredAt).toLocaleString()), /* @__PURE__ */ import_react2.default.createElement("td", null, /* @__PURE__ */ import_react2.default.createElement("span", { className: "badge badge-delivered" }, l.EventType || l.eventType)), /* @__PURE__ */ import_react2.default.createElement("td", { style: { fontFamily: "monospace", fontSize: "0.85em", maxWidth: "400px", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" } }, l.RawPayloadJson || l.rawPayloadJson)))))));
    }
    if (activeTab === "broadcasts") {
      const handleSendBroadcast = async () => {
        if (!selectedBroadcastTemplate) {
          alert("Please select a template");
          return;
        }
        try {
          const res = await fetch(`${API_BASE}/broadcast`, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({
              TemplateId: parseInt(selectedBroadcastTemplate),
              CategoryId: selectedBroadcastCategory ? parseInt(selectedBroadcastCategory) : null
            })
          });
          const data = await res.json();
          alert(data.message);
          fetchData();
        } catch (err) {
          alert("Failed to queue broadcast");
        }
      };
      return /* @__PURE__ */ import_react2.default.createElement("div", null, /* @__PURE__ */ import_react2.default.createElement("div", { className: "card", style: { marginBottom: "24px" } }, /* @__PURE__ */ import_react2.default.createElement("h3", { className: "card-title" }, "New WhatsApp Campaign"), /* @__PURE__ */ import_react2.default.createElement("div", { style: { display: "flex", gap: "16px", alignItems: "flex-end" } }, /* @__PURE__ */ import_react2.default.createElement("div", { style: { flex: 1 } }, /* @__PURE__ */ import_react2.default.createElement("label", { style: { display: "block", marginBottom: "8px", fontWeight: 500 } }, "Target Audience"), /* @__PURE__ */ import_react2.default.createElement(
        "select",
        {
          className: "input",
          value: selectedBroadcastCategory,
          onChange: (e) => setSelectedBroadcastCategory(e.target.value),
          style: { width: "100%", padding: "10px", borderRadius: "6px", border: "1px solid #e2e8f0" }
        },
        /* @__PURE__ */ import_react2.default.createElement("option", { value: "" }, "All Contacts"),
        (categories || []).map((c) => /* @__PURE__ */ import_react2.default.createElement("option", { key: c.Id || c.id, value: c.Id || c.id }, c.Name || c.name))
      )), /* @__PURE__ */ import_react2.default.createElement("div", { style: { flex: 1 } }, /* @__PURE__ */ import_react2.default.createElement("label", { style: { display: "block", marginBottom: "8px", fontWeight: 500 } }, "Message Template"), /* @__PURE__ */ import_react2.default.createElement(
        "select",
        {
          className: "input",
          value: selectedBroadcastTemplate,
          onChange: (e) => setSelectedBroadcastTemplate(e.target.value),
          style: { width: "100%", padding: "10px", borderRadius: "6px", border: "1px solid #e2e8f0" }
        },
        /* @__PURE__ */ import_react2.default.createElement("option", { value: "" }, "Select a template..."),
        (templatesList || []).map((t) => /* @__PURE__ */ import_react2.default.createElement("option", { key: t.Id || t.id, value: t.Id || t.id }, t.Name || t.name))
      )), /* @__PURE__ */ import_react2.default.createElement("div", null, /* @__PURE__ */ import_react2.default.createElement("button", { className: "btn btn-primary", onClick: handleSendBroadcast, style: { width: "100%", height: "42px" } }, /* @__PURE__ */ import_react2.default.createElement(import_lucide_react2.Send, { size: 16 }), " Launch Campaign")))), /* @__PURE__ */ import_react2.default.createElement("div", { className: "card" }, /* @__PURE__ */ import_react2.default.createElement("h3", { className: "card-title" }, "Past Campaigns"), /* @__PURE__ */ import_react2.default.createElement("div", { className: "data-table-container" }, /* @__PURE__ */ import_react2.default.createElement("table", { className: "data-table" }, /* @__PURE__ */ import_react2.default.createElement("thead", null, /* @__PURE__ */ import_react2.default.createElement("tr", null, /* @__PURE__ */ import_react2.default.createElement("th", null, "Started At"), /* @__PURE__ */ import_react2.default.createElement("th", null, "Template"), /* @__PURE__ */ import_react2.default.createElement("th", null, "Total Queued"), /* @__PURE__ */ import_react2.default.createElement("th", null, "Delivered"), /* @__PURE__ */ import_react2.default.createElement("th", null, "Pending"))), /* @__PURE__ */ import_react2.default.createElement("tbody", null, (broadcasts || []).map((b, i) => /* @__PURE__ */ import_react2.default.createElement("tr", { key: i }, /* @__PURE__ */ import_react2.default.createElement("td", null, new Date(b.StartedAt || b.startedAt).toLocaleString()), /* @__PURE__ */ import_react2.default.createElement("td", null, b.TemplateName || b.templateName), /* @__PURE__ */ import_react2.default.createElement("td", null, b.TotalSent || b.totalSent), /* @__PURE__ */ import_react2.default.createElement("td", null, /* @__PURE__ */ import_react2.default.createElement("span", { className: "badge badge-delivered" }, b.Delivered || b.delivered)), /* @__PURE__ */ import_react2.default.createElement("td", null, /* @__PURE__ */ import_react2.default.createElement("span", { className: "badge badge-pending" }, b.Pending || b.pending)))), broadcasts.length === 0 && /* @__PURE__ */ import_react2.default.createElement("tr", null, /* @__PURE__ */ import_react2.default.createElement("td", { colSpan: "5", style: { textAlign: "center", padding: "24px" } }, "No broadcasts sent yet.")))))));
    }
    return null;
  };
  return /* @__PURE__ */ import_react2.default.createElement("div", { className: "app-container" }, /* @__PURE__ */ import_react2.default.createElement("aside", { className: "sidebar" }, /* @__PURE__ */ import_react2.default.createElement("div", { className: "sidebar-header" }, /* @__PURE__ */ import_react2.default.createElement(import_lucide_react2.Hotel, { color: "var(--primary)" }), " HotelAuto"), /* @__PURE__ */ import_react2.default.createElement("nav", { style: { display: "flex", flexDirection: "column", gap: "8px", padding: "12px 0" } }, /* @__PURE__ */ import_react2.default.createElement("div", { className: `nav-item ${activeTab === "dashboard" ? "active" : ""}`, onClick: () => setActiveTab("dashboard") }, /* @__PURE__ */ import_react2.default.createElement(import_lucide_react2.LayoutDashboard, { size: 20 }), " Dashboard"), /* @__PURE__ */ import_react2.default.createElement("div", { className: `nav-item ${activeTab === "contacts" ? "active" : ""}`, onClick: () => setActiveTab("contacts") }, /* @__PURE__ */ import_react2.default.createElement(import_lucide_react2.Users, { size: 20 }), " Contacts"), /* @__PURE__ */ import_react2.default.createElement("div", { className: `nav-item ${activeTab === "templates" ? "active" : ""}`, onClick: () => setActiveTab("templates") }, /* @__PURE__ */ import_react2.default.createElement(import_lucide_react2.MessagesSquare, { size: 20 }), " Templates"), /* @__PURE__ */ import_react2.default.createElement("div", { className: `nav-item ${activeTab === "broadcasts" ? "active" : ""}`, onClick: () => setActiveTab("broadcasts") }, /* @__PURE__ */ import_react2.default.createElement(import_lucide_react2.Megaphone, { size: 20 }), " Campaigns"), /* @__PURE__ */ import_react2.default.createElement("div", { className: `nav-item ${activeTab === "rules" ? "active" : ""}`, onClick: () => setActiveTab("rules") }, /* @__PURE__ */ import_react2.default.createElement(import_lucide_react2.Workflow, { size: 20 }), " Automation"), /* @__PURE__ */ import_react2.default.createElement("div", { className: `nav-item ${activeTab === "logs" ? "active" : ""}`, onClick: () => setActiveTab("logs") }, /* @__PURE__ */ import_react2.default.createElement(import_lucide_react2.ScrollText, { size: 20 }), " Event Logs"))), /* @__PURE__ */ import_react2.default.createElement("main", { className: "main-content" }, /* @__PURE__ */ import_react2.default.createElement("header", { className: "topbar" }, /* @__PURE__ */ import_react2.default.createElement("div", { className: "topbar-title" }, activeTab.charAt(0).toUpperCase() + activeTab.slice(1))), /* @__PURE__ */ import_react2.default.createElement("div", { className: "page-content" }, renderContent())));
}
/*! Bundled license information:

papaparse/papaparse.js:
  (* @license
  Papa Parse
  v5.5.4
  https://github.com/mholt/PapaParse
  License: MIT
  *)
*/
