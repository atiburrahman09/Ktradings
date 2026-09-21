(function () {
    const body = document.querySelector('#productUnitsTable tbody');
    if (!body) return;

    function reindex() {
        body.querySelectorAll('tr').forEach(function (row, index) {
            row.querySelectorAll('[name]').forEach(function (element) {
                element.name = element.name.replace(/Units\[\d+\]/, 'Units[' + index + ']');
            });
        });
    }

    function bind() {
        body.querySelectorAll('.remove-unit').forEach(function (button) {
            button.onclick = function () { button.closest('tr').remove(); reindex(); };
        });
        body.querySelectorAll('.base-unit').forEach(function (radio) {
            radio.onchange = function () {
                if (radio.checked) {
                    body.querySelectorAll('.base-unit').forEach(function (other) { if (other !== radio) other.checked = false; });
                    radio.closest('tr').querySelector('.factor').value = '1';
                }
            };
        });
    }

    document.getElementById('addProductUnit').onclick = function () {
        const index = body.querySelectorAll('tr').length;
        const row = document.createElement('tr');
        row.innerHTML = '<input type="hidden" name="Units[' + index + '].Id" value="00000000-0000-0000-0000-000000000000" />' +
            '<td><input name="Units[' + index + '].Name" class="form-control" /></td>' +
            '<td><input name="Units[' + index + '].ConversionFactor" value="1" type="number" min="0.0001" step="0.0001" class="form-control factor" /></td>' +
            '<td><input name="Units[' + index + '].SellingPrice" value="0" type="number" min="0" step="0.0001" class="form-control" /></td>' +
            '<td><input name="Units[' + index + '].PurchaseCost" value="0" type="number" min="0" step="0.0001" class="form-control" /></td>' +
            '<td><input name="Units[' + index + '].SKU" class="form-control" /></td>' +
            '<td class="text-center"><input name="Units[' + index + '].IsBaseUnit" value="true" type="radio" class="form-check-input base-unit" /></td>' +
            '<td><button type="button" class="btn btn-sm btn-danger remove-unit">Remove</button></td>';
        body.appendChild(row);
        bind();
    };
    bind();
})();
