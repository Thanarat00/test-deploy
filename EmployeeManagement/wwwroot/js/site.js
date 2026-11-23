"use strict";

// HTML constraints work without JavaScript; these messages add Thai field feedback.
document.querySelectorAll("[data-employee-form]").forEach(form => {
    form.noValidate = true;
    const fields = Array.from(form.querySelectorAll("input:not([type=hidden]):not([type=checkbox])"));
    const errorFor = field => document.getElementById(`${field.id}-error`);

    function validate(field) {
        field.setCustomValidity("");
        let message = "";
        if (field.required && !field.value.trim()) message = "กรุณากรอกข้อมูลช่องนี้";
        else if (field.validity.typeMismatch) message = "กรุณาระบุอีเมลให้ถูกต้อง";
        else if (field.validity.badInput) message = "กรุณาระบุตัวเลขให้ถูกต้อง";
        else if (field.validity.rangeUnderflow || field.validity.rangeOverflow) message = "เงินเดือนต้องอยู่ระหว่าง 0 ถึง 9,999,999,999.99 บาท";
        else if (field.validity.stepMismatch) message = "เงินเดือนต้องมีทศนิยมไม่เกิน 2 ตำแหน่ง";
        else if (field.validity.tooLong || (field.maxLength > 0 && field.value.length > field.maxLength)) message = `กรุณากรอกไม่เกิน ${field.maxLength} ตัวอักษร`;
        field.setCustomValidity(message);
        field.classList.toggle("is-invalid", !!message);
        field.classList.remove("input-validation-error");
        field.setAttribute("aria-invalid", message ? "true" : "false");
        const error = errorFor(field);
        if (error) error.textContent = message;
        return field.checkValidity();
    }

    fields.forEach(field => {
        const error = errorFor(field);
        if (error && error.textContent.trim()) field.setAttribute("aria-invalid", "true");
        field.addEventListener("blur", () => validate(field));
        field.addEventListener("input", () => {
            if (field.classList.contains("is-invalid") || field.getAttribute("aria-invalid") === "true") validate(field);
        });
    });
    form.addEventListener("submit", event => {
        fields.forEach(field => {
            if (field.type !== "number") field.value = field.value.trim();
        });
        const invalid = fields.filter(field => !validate(field));
        const summary = form.querySelector(".client-validation-summary");
        if (invalid.length) {
            event.preventDefault();
            summary?.classList.remove("d-none");
            invalid[0].focus();
            return;
        }
        summary?.classList.add("d-none");
        const button = form.querySelector("button[type=submit]");
        if (button) { button.disabled = true; button.textContent = "กำลังบันทึก…"; }
    });
});

document.querySelectorAll("[data-delete-form]").forEach(form => {
    form.addEventListener("submit", () => {
        const button = form.querySelector("button[type=submit]");
        if (button) { button.disabled = true; button.textContent = "กำลังลบ…"; }
    });
});
