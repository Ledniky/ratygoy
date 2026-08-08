research-server-ui-title = Сервер RnD
research-server-ui-label-clients = Машины

research-server-ui-client-entry = {$name} [{$pos}]{$allowed ->
    [true] {$connected ->
        [true]  — подключено
        *[other]  — разрешено
    }
    *[other] {""}
}