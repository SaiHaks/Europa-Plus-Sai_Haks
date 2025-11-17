# Файл: Resources/Locale/ru-RU/Europa/Soulbreakers/soulbreaker-collar.ftl

## Сообщения для системы ошейников соулбрейкеров

## Сообщения при попытке взаимодействия
soulbreaker-collar-cannot-interact-message = Вы не можете этого сделать!
soulbreaker-collar-cannot-remove-collar-too-far-message = Вы слишком далеко, чтобы снять ошейник.

## Сообщения при начале снятия ошейника
soulbreaker-collar-start-unenslaving-self = Вы начинаете мучительно пытаться снять ошейник.
soulbreaker-collar-start-unenslaving-observer = { $user } начинает снимать ошейник с { $target }!
soulbreaker-collar-start-unenslaving-self-observer = { $user } начинает снимать ошейник с { REFLEXIVE($target) } себя!
soulbreaker-collar-start-unenslaving-target-message = Вы начинаете снимать ошейник с { $targetName }.
soulbreaker-collar-start-unenslaving-by-other-message = { $otherName } начинает снимать с вас ошейник!

## Сообщения при успешном снятии ошейника
soulbreaker-collar-remove-collar-success-message = Вы успешно снимаете ошейник.
soulbreaker-collar-remove-collar-push-success-message = Вы успешно снимаете ошейник и толкаете { $otherName } на пол.
soulbreaker-collar-remove-collar-by-other-success-message = { $otherName } снимает с вас ошейник.
soulbreaker-collar-remove-collar-fail-message = Вам не удалось снять ошейник.

## Сообщения при надевании ошейника
soulbreaker-collar-cannot-enslave-themself = Вы не можете надеть ошейник на себя!
soulbreaker-collar-cannot-drop-collar = Вы не можете отпустить ошейник!
soulbreaker-collar-target-flying-error = { $targetName } летает, вы не можете надеть ошейник!
soulbreaker-collar-too-far-away-error = Слишком далеко!

## Сообщения при начале надевания ошейника
soulbreaker-collar-start-enslaving-self-observer = { $user } начинает надевать ошейник на { REFLEXIVE($target) } себя!
soulbreaker-collar-start-enslaving-observer = { $user } начинает надевать ошейник на { $target }!
soulbreaker-collar-start-enslaving-target-message = Вы начинаете надевать ошейник на { $targetName }.
soulbreaker-collar-start-enslaving-by-other-message = { $otherName } начинает надевать на вас ошейник!
soulbreaker-collar-target-self = Вы начинаете надевать ошейник на себя.

## Сообщения при успешном надевании ошейника
soulbreaker-collar-enslave-self-observer-success-message = { $user } надел ошейник на { REFLEXIVE($target) } себя!
soulbreaker-collar-enslave-observer-success-message = { $user } надел ошейник на { $target }!
soulbreaker-collar-enslave-self-success-message = Вы надели на себя ошейник.
soulbreaker-collar-enslave-other-success-message = Вы надели ошейник на { $otherName }.
soulbreaker-collar-enslave-by-other-success-message = { $otherName } надел на вас ошейник.

## Сообщения при прерывании надевания ошейника
soulbreaker-collar-enslave-interrupt-self-message = Вам не удалось надеть ошейник.
soulbreaker-collar-enslave-interrupt-message = Вам не удалось надеть ошейник на { $targetName }.
soulbreaker-collar-enslave-interrupt-other-message = { $otherName } не удалось надеть на вас ошейник.

## Защита от ошейника
soulbreaker-collar-protection-reason =
    Вы не можете надеть ошейник на { GENDER($identity) ->
    [male] него
    [female] неё
    [epicene] них
    *[neuter] это
        }!

## Глаголы
unenslave-verb-get-data-text = Снять ошейник

soulbreaker-collar-block-action = Ошейник запрещает сделать это!
soulbreaker-collar-authorization-error-equip = Ошейник не закрепляется на шее!
soulbreaker-collar-authorization-error-unequip = Ошейник не открывается!
