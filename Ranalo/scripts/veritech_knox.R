# Send IMEIs to Knox Guard through Veritech, check them, and lock them --
# for Samsung phones whose Nuovo lock may have been removed.
#
# Same calls as the app (Ranalo/VeriTechClient/VeritechApiClient.cs). The
# login is the Veritech API key, read from Ranalo/appsettings.Development.json
# (Veritech:BaseUrl, Veritech:ApiKey) -- it is never printed.
#
# Devices come from a CSV with a column "imei" (other columns, e.g. account,
# are kept for the results). See imeis_example.csv.
#
# From Terminal (in this folder):
#   Rscript veritech_knox.R upload  imeis.csv     send the IMEIs to Veritech -> Knox
#   Rscript veritech_knox.R status  <txId>        result of an upload
#   Rscript veritech_knox.R devices imeis.csv     is each IMEI in Knox, and its status
#     -> approve the pending phones in the Knox Guard console (no Veritech call
#        for it; the app does it with the Knox API at enrolment)
#   Rscript veritech_knox.R lock    imeis.csv     lock them (once approved; a phone
#                                                 locks when it next connects)
#   Rscript veritech_knox.R unlock  imeis.csv     undo the lock
#
# Or in RStudio: source("veritech_knox.R") and call the functions, e.g.
#   d <- read_imeis("imeis.csv"); upload_imeis(d$imei); check_devices(d)
#
# Each run also writes its results to results_<step>_<time>.csv.
# Only Samsung phones can be caught by Knox.

suppressPackageStartupMessages({
  library(httr)
  library(jsonlite)
})

LOCK_MESSAGE <- "This phone is locked by Ranalo Credit. Please call us to settle your account."
UPLOAD_BATCH <- 50   # IMEIs per upload call

# --- Login ------------------------------------------------------------------

script_dir <- function() {
  args <- commandArgs(trailingOnly = FALSE)
  file <- sub("^--file=", "", args[grep("^--file=", args)])
  if (length(file) == 1) return(dirname(normalizePath(file)))
  if (!is.null(sys.frames()[[1]]$ofile)) return(dirname(normalizePath(sys.frames()[[1]]$ofile)))
  getwd()
}

read_veritech_config <- function(path = file.path(script_dir(), "..", "Ranalo", "appsettings.Development.json")) {
  lines <- readLines(path, warn = FALSE)
  lines <- lines[!grepl("^\\s*//", lines)]            # the file has // comment lines
  cfg <- fromJSON(paste(lines, collapse = "\n"))$Veritech
  if (is.null(cfg$BaseUrl) || is.null(cfg$ApiKey)) stop("Veritech:BaseUrl / Veritech:ApiKey missing in ", path)
  list(base = sub("/*$", "/", cfg$BaseUrl), key = cfg$ApiKey)
}

VERITECH <- read_veritech_config()

# --- API --------------------------------------------------------------------

veritech <- function(method, path, body = NULL) {
  res <- VERB(method, paste0(VERITECH$base, path),
              add_headers(`x-vtkdp-key` = VERITECH$key, Accept = "application/json"),
              body = if (is.null(body)) NULL else toJSON(body, auto_unbox = TRUE),
              content_type_json(), timeout(60))
  text <- content(res, as = "text", encoding = "UTF-8")
  parsed <- tryCatch(fromJSON(text, simplifyVector = FALSE), error = function(e) NULL)
  list(http = status_code(res), body = parsed, text = text)
}

# The app reads Veritech's JSON case-insensitively; so do we.
field <- function(x, name) {
  if (!is.list(x)) return(NULL)
  hit <- which(tolower(names(x)) == tolower(name))
  if (length(hit)) x[[hit[1]]] else NULL
}
txt <- function(x) if (is.null(x)) NA_character_ else as.character(x)

# --- Steps ------------------------------------------------------------------

read_imeis <- function(csv) {
  d <- read.csv(csv, colClasses = "character", strip.white = TRUE)
  names(d) <- tolower(names(d))
  if (!"imei" %in% names(d)) stop(csv, " needs a column named 'imei'")
  d$imei <- gsub("[^0-9]", "", d$imei)
  bad <- nchar(d$imei) != 15
  if (any(bad)) warning("Skipping IMEIs that are not 15 digits: ", paste(d$imei[bad], collapse = ", "))
  unique(d[!bad, , drop = FALSE])
}

upload_imeis <- function(imeis) {
  batches <- split(imeis, ceiling(seq_along(imeis) / UPLOAD_BATCH))
  out <- do.call(rbind, lapply(batches, function(b) {
    r <- veritech("POST", "devices/upload", list(devices = I(b)))
    data <- field(r$body, "data")
    data.frame(imeis = paste(b, collapse = " "), http = r$http,
               transaction_id = txt(field(data, "transaction_id")),
               status = txt(field(data, "status")),
               message = txt(field(data, "message") %||% field(r$body, "message") %||% r$text),
               stringsAsFactors = FALSE)
  }))
  cat("Keep the transaction id(s) for: Rscript veritech_knox.R status <txId>\n")
  out
}

transaction_status <- function(tx_id) {
  r <- veritech("GET", paste0("devices/transaction-status/", tx_id))
  data <- field(r$body, "data")
  data.frame(transaction_id = tx_id, http = r$http,
             status = txt(field(data, "status")), result = txt(field(data, "result")),
             message = txt(field(data, "message") %||% field(r$body, "message") %||% r$text),
             stringsAsFactors = FALSE)
}

check_devices <- function(d) {
  r <- veritech("GET", "devices")
  rows <- field(field(r$body, "data"), "devicelist")
  if (r$http != 200 || is.null(rows)) stop("Could not list devices (HTTP ", r$http, "): ", r$text)
  known <- setNames(vapply(rows, function(x) txt(field(x, "status")), ""),
                    vapply(rows, function(x) txt(field(x, "imeinumber")), ""))
  d$knox_status <- ifelse(d$imei %in% names(known), known[d$imei], "not in Veritech/Knox")
  d
}

lock_imeis <- function(d, message = LOCK_MESSAGE) {
  d$http <- NA_integer_; d$result <- NA_character_; d$message <- NA_character_
  for (i in seq_len(nrow(d))) {
    r <- veritech("POST", "knox-guard/lock-device", list(ImeiNumber = d$imei[i], LockScreenMessage = message))
    data <- field(r$body, "data")
    d$http[i] <- r$http
    d$result[i] <- txt(field(data, "result") %||% field(data, "status"))
    d$message[i] <- txt(field(data, "message") %||% field(r$body, "message") %||% r$text)
    cat(d$imei[i], "->", r$http, d$result[i], "\n")
  }
  d
}

unlock_imeis <- function(d) {
  d$http <- NA_integer_; d$result <- NA_character_; d$message <- NA_character_
  for (i in seq_len(nrow(d))) {
    r <- veritech("POST", "knox-guard/unlock-device",
                  list(ImeiNumber = d$imei[i], RelockTimestamp = 0, LockScreenMessage = ""))
    data <- field(r$body, "data")
    d$http[i] <- r$http
    d$result[i] <- txt(field(data, "result") %||% field(data, "status"))
    d$message[i] <- txt(field(data, "message") %||% field(r$body, "message") %||% r$text)
    cat(d$imei[i], "->", r$http, d$result[i], "\n")
  }
  d
}

`%||%` <- function(a, b) if (is.null(a)) b else a

save_results <- function(x, step) {
  f <- sprintf("results_%s_%s.csv", step, format(Sys.time(), "%Y%m%d_%H%M%S"))
  write.csv(x, f, row.names = FALSE)
  print(x, row.names = FALSE)
  cat("\nSaved", f, "\n")
}

# --- Command line -----------------------------------------------------------

if (sys.nframe() == 0) {
  args <- commandArgs(trailingOnly = TRUE)
  step <- if (length(args)) args[1] else ""
  arg <- if (length(args) > 1) args[2] else NA

  confirm <- function(what, n) {
    cat(sprintf("About to %s %d phone(s). Type yes to continue: ", what, n))
    if (tolower(trimws(readLines("stdin", n = 1))) != "yes") stop("Cancelled.")
  }

  if (step == "upload") {
    d <- read_imeis(arg); confirm("upload to Knox", nrow(d))
    save_results(upload_imeis(d$imei), "upload")
  } else if (step == "status") {
    if (is.na(arg)) stop("Usage: Rscript veritech_knox.R status <transactionId>")
    save_results(transaction_status(arg), "status")
  } else if (step == "devices") {
    save_results(check_devices(read_imeis(arg)), "devices")
  } else if (step == "lock") {
    d <- read_imeis(arg); confirm("LOCK", nrow(d))
    save_results(lock_imeis(d), "lock")
  } else if (step == "unlock") {
    d <- read_imeis(arg); confirm("UNLOCK", nrow(d))
    save_results(unlock_imeis(d), "unlock")
  } else {
    cat(readLines(file.path(script_dir(), "veritech_knox.R"), n = 25), sep = "\n")
  }
}
