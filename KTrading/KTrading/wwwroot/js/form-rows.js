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

    function bindTable(tableId, addBtnId){
        var table = document.getElementById(tableId);
        if(!table) return;
        var tbody = table.querySelector('tbody');
        var template = tbody.querySelector('.template');
        var addBtn = document.getElementById(addBtnId);

        addBtn && addBtn.addEventListener('click', function(){
            var clone = template.cloneNode(true);
            clone.classList.remove('template','d-none');
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
        tbody.addEventListener('input', function(e){
            if(e.target.matches('.qty') || e.target.matches('.unitprice')){
                var row = e.target.closest('tr');
                var qty = parseFloat(row.querySelector('.qty').value) || 0;
                var price = parseFloat(row.querySelector('.unitprice').value) || 0;
                var total = qty * price;
                var lt = row.querySelector('.linetotal');
                lt.value = total.toFixed(2);
                if(window.computeAll) window.computeAll();
            }
        });

        document.getElementById('addItem').addEventListener('click', function(){
            // clone template row and set unit prices based on selected option data-price
            var template = tbody.querySelector('.template');
            var clone = template.cloneNode(true);
            clone.classList.remove('template','d-none');
            // set default unit price if first option has data-price
            var sel = clone.querySelector('.product-select');
            if(sel){
                var opt = sel.options[sel.selectedIndex];
                if(opt && opt.dataset.price) clone.querySelector('.unitprice').value = parseFloat(opt.dataset.price || 0).toFixed(2);
            }
            tbody.appendChild(clone);
            updateIndexes(tbody, 'tr:not(.template)');
            // bind product change to set price
            bindProductChange(tbody);
        });

        function bindProductChange(tbody){
            var selects = tbody.querySelectorAll('.product-select');
            selects.forEach(function(sel){
                sel.onchange = function(){
                    var opt = sel.options[sel.selectedIndex];
                    var row = sel.closest('tr');
                    if(opt && opt.dataset.price){
                        row.querySelector('.unitprice').value = parseFloat(opt.dataset.price).toFixed(2);
                        var qty = parseFloat(row.querySelector('.qty').value)||0;
                        row.querySelector('.linetotal').value = (qty * parseFloat(opt.dataset.price||0)).toFixed(2);
                        if(window.computeAll) window.computeAll();
                    }
                }
            });
        }

        bindProductChange(tbody);
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
    }

    document.addEventListener('DOMContentLoaded', function(){
        bindTable('returnItemsTable','addReturnItem');
        bindSalesOrderTable();
        bindTable('itemsTable','addItem');
    });
})();
