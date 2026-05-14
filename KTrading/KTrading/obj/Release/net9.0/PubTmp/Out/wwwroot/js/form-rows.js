// Simple helper to duplicate template rows and update name indexes
(function(){
    function updateIndexes(container, selector){
        var rows = container.querySelectorAll(selector);
        rows.forEach(function(row, idx){
            var inputs = row.querySelectorAll('input, select, textarea');
            inputs.forEach(function(inp){
                if(inp.name){
                    // replace Items[0] with Items[idx]
                    inp.name = inp.name.replace(/Items\[\d+\]/, 'Items['+idx+']');
                }
            });
        });
    }

    function enableRowControls(row){
        row.querySelectorAll('input, select, textarea').forEach(function(input){
            input.disabled = false;
        });
    }

    function bindTable(tableId, addBtnId){
        var table = document.getElementById(tableId);
        if(!table) return;
        var tbody = table.querySelector('tbody');
        var template = tbody.querySelector('.template');
        var addBtn = document.getElementById(addBtnId);

        addBtn && addBtn.addEventListener('click', function(){
            var clone = template.cloneNode(true);
            clone.classList.remove('template','d-none');
            enableRowControls(clone);
            tbody.appendChild(clone);
            updateIndexes(tbody, 'tr:not(.template)');
            bindRemoveButtons(tbody);
        });

        function bindRemoveButtons(tbody){
            var removes = tbody.querySelectorAll('.remove-row');
            removes.forEach(function(btn){
                btn.onclick = function(){
                    var row = btn.closest('tr');
                    row.remove();
                    updateIndexes(tbody, 'tr:not(.template)');
                    if(window.computeAll) window.computeAll();
                }
            });
        }

        bindRemoveButtons(tbody);
    }

    // Specific for sales order item table to compute totals
    function bindSalesOrderTable(){
        var table = document.getElementById('itemsTable');
        if(!table) return;
        var tbody = table.querySelector('tbody');
        var productOptions = Array.prototype.map.call(tbody.querySelector('.template .product-select').options, function(option){
            return {
                value: option.value,
                text: option.text,
                category: normalizeKey(option.dataset.category),
                price: option.dataset.price || '',
                stock: option.dataset.stock || ''
            };
        });
        console.log('[SalesOrder] Product options cache:', productOptions);
        tbody.addEventListener('input', function(e){
            if(e.target.matches('.qty') || e.target.matches('.unitprice')){
                var row = e.target.closest('tr');
                var qty = parseFloat(row.querySelector('.qty').value) || 0;
                var price = parseFloat(row.querySelector('.unitprice').value) || 0;
                var total = qty * price;
                var lt = row.querySelector('.linetotal');
                lt.value = total.toFixed(2);
                validateRowStock(row);
                if(window.computeAll) window.computeAll();
            }
        });

        tbody.addEventListener('change', function(e){
            var row = e.target.closest('tr');
            if(!row || row.classList.contains('template')) return;

            if(e.target.matches('.category-select')){
                console.log('[SalesOrder] Category changed:', {
                    selectedCategory: e.target.value,
                    selectedText: e.target.options[e.target.selectedIndex] && e.target.options[e.target.selectedIndex].text
                });
                filterProductsForRow(row, true);
                return;
            }

            if(e.target.matches('.product-select')){
                console.log('[SalesOrder] Product changed:', {
                    selectedProduct: e.target.value,
                    selectedText: e.target.options[e.target.selectedIndex] && e.target.options[e.target.selectedIndex].text,
                    selectedCategory: e.target.options[e.target.selectedIndex] && e.target.options[e.target.selectedIndex].dataset.category
                });
                updateProductPrice(row);
            }
        });

        document.getElementById('addItem').addEventListener('click', function(){
            // clone template row and set unit prices based on selected option data-price
            var template = tbody.querySelector('.template');
            var clone = template.cloneNode(true);
            clone.classList.remove('template','d-none');
            enableRowControls(clone);
            // set default unit price if first option has data-price
            var sel = clone.querySelector('.product-select');
            if(sel){
                var opt = sel.options[sel.selectedIndex];
                if(opt && opt.dataset.price) clone.querySelector('.unitprice').value = parseFloat(opt.dataset.price || 0).toFixed(2);
            }
            tbody.appendChild(clone);
            updateIndexes(tbody, 'tr:not(.template)');
            filterProductsForRow(clone, false);
        });

        function updateProductPrice(row){
            var productSelect = row.querySelector('.product-select');
            var opt = productSelect && productSelect.options[productSelect.selectedIndex];
            if(opt && opt.dataset.price){
                row.querySelector('.unitprice').value = parseFloat(opt.dataset.price).toFixed(2);
                var qty = parseFloat(row.querySelector('.qty').value)||0;
                row.querySelector('.linetotal').value = (qty * parseFloat(opt.dataset.price||0)).toFixed(2);
                validateRowStock(row);
                if(window.computeAll) window.computeAll();
            } else {
                row.querySelector('.unitprice').value = '';
                row.querySelector('.linetotal').value = '';
                if(window.computeAll) window.computeAll();
            }
        }

        function filterProductsForRow(row, resetProduct){
            if(!row) return;

            var categorySelect = row.querySelector('.category-select');
            var productSelect = row.querySelector('.product-select');
            if(!categorySelect || !productSelect) return;

            var selectedCategory = normalizeKey(categorySelect.value);
            var currentValue = resetProduct ? '' : productSelect.value;
            productSelect.innerHTML = '';

            console.log('[SalesOrder] Filtering products for row:', {
                selectedCategory: selectedCategory,
                resetProduct: resetProduct,
                currentValue: currentValue,
                allProductCategories: productOptions.map(function(source){
                    return {
                        text: source.text,
                        category: source.category,
                        value: source.value
                    };
                })
            });

            productOptions.forEach(function(source){
                if(source.value && selectedCategory && source.category !== selectedCategory){
                    return;
                }

                var option = document.createElement('option');
                option.value = source.value;
                option.text = source.text;
                option.dataset.category = source.category;
                option.dataset.price = source.price;
                option.dataset.stock = source.stock;
                productSelect.appendChild(option);
            });

            console.log('[SalesOrder] Product options after filter:', Array.prototype.map.call(productSelect.options, function(option){
                return {
                    text: option.text,
                    category: option.dataset.category || '',
                    value: option.value
                };
            }));

            if(currentValue && Array.prototype.some.call(productSelect.options, function(option){ return option.value === currentValue; })){
                productSelect.value = currentValue;
            } else if(currentValue) {
                productSelect.value = '';
                row.querySelector('.unitprice').value = '';
                row.querySelector('.linetotal').value = '';
                validateRowStock(row);
                if(window.computeAll) window.computeAll();
            }
        }

        function filterAllProductRows(){
            tbody.querySelectorAll('tr:not(.template)').forEach(function(row){
                filterProductsForRow(row, false);
            });
        }

        filterAllProductRows();
    }

    function normalizeKey(value){
        return (value || '').toString().trim().toLowerCase();
    }

    function validateRowStock(row){
        var select = row.querySelector('.product-select');
        var qtyInput = row.querySelector('.qty');
        if(!select || !qtyInput) return;

        var option = select.options[select.selectedIndex];
        var stock = parseFloat(option && option.dataset.stock) || 0;
        var qty = parseFloat(qtyInput.value) || 0;

        qtyInput.setCustomValidity(qty > stock ? 'Only ' + stock.toFixed(2) + ' is available in stock.' : '');
    }

    window.computeAll = function(){
        var subtotal = 0;
        var rows = document.querySelectorAll('#itemsTable tbody tr:not(.template)');
        rows.forEach(function(r){
            var v = parseFloat(r.querySelector('.linetotal').value) || 0;
            subtotal += v;
        });
        var el = document.getElementById('subtotal');
        if(el) el.textContent = subtotal.toFixed(2);

        var paidInput = document.getElementById('SalesOrder_PaidAmount');
        var commissionInput = document.getElementById('SalesOrder_Commission');
        var paid = parseFloat(paidInput && paidInput.value) || 0;
        var commission = parseFloat(commissionInput && commissionInput.value) || 0;
        var dueEl = document.getElementById('dueAmount');
        var netEl = document.getElementById('netAfterExpenses');
        if(dueEl) dueEl.value = Math.max(subtotal - paid, 0).toFixed(2);
        if(netEl) netEl.textContent = (subtotal - commission).toFixed(2);
    }

    document.addEventListener('DOMContentLoaded', function(){
        bindTable('returnItemsTable','addReturnItem');
        bindSalesOrderTable();
        document.querySelectorAll('.money-input').forEach(function(input){
            input.addEventListener('input', function(){
                if(window.computeAll) window.computeAll();
            });
        });
        if(window.computeAll) window.computeAll();
    });
})();
