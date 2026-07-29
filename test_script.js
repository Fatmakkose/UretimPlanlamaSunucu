    function switchTab(tabId, element) {
        // Tüm sekmeleri inaktif yap
        document.querySelectorAll('.plan-tab').forEach(t => t.classList.remove('active'));
        document.querySelectorAll('.tab-content').forEach(c => c.classList.remove('active'));
        
        // Seçileni aktif yap
        element.classList.add('active');
        document.getElementById('tab-' + tabId).classList.add('active');
    }

    // Sipariş arama ve filtreleme
    var currentOrderFilter = 'all';

    function setOrderFilter(mode) {
        currentOrderFilter = mode;

        // Buton stillerini güncelle
        var btnAll = document.getElementById('filterAll');
        var btnActive = document.getElementById('filterActive');
        var btnPassive = document.getElementById('filterPassive');

        // Tüm butonları pasif yap
        [btnAll, btnActive, btnPassive].forEach(function(b) {
            b.style.background = 'white';
            b.style.borderColor = '#e2e8f0';
            b.style.color = '#475569';
        });

        // Seçili butonu aktif yap
        if (mode === 'all') {
            btnAll.style.background = '#0369a1';
            btnAll.style.borderColor = '#0369a1';
            btnAll.style.color = 'white';
        } else if (mode === 'active') {
            btnActive.style.background = '#dcfce7';
            btnActive.style.borderColor = '#16a34a';
            btnActive.style.color = '#16a34a';
        } else {
            btnPassive.style.background = '#f1f5f9';
            btnPassive.style.borderColor = '#94a3b8';
            btnPassive.style.color = '#475569';
        }

        filterOrders();
    }

    function filterOrders() {
        var searchEl = document.getElementById('orderSearch');
        var searchTerm = searchEl ? searchEl.value.toLowerCase() : '';
        var items = document.querySelectorAll('.order-list-item');

        items.forEach(function(item) {
            var text = item.textContent.toLowerCase();
            var isActive = item.getAttribute('data-active') === 'true';
            var matchesSearch = text.includes(searchTerm);
            var matchesFilter = (currentOrderFilter === 'all') ||
                                (currentOrderFilter === 'active' && isActive) ||
                                (currentOrderFilter === 'passive' && !isActive);

            item.style.display = (matchesSearch && matchesFilter) ? 'block' : 'none';
        });
    }

    document.getElementById('orderSearch').addEventListener('keyup', filterOrders);

    // Yardımcı Fonksiyon: Sayı çevirici (11.800,25 gibi Türkçe formatları destekler)
    function parseTurkishFloat(str) {
        if (!str) return 0;
        // Eğer içinde hem nokta hem virgül varsa (örn: 11.800,25) -> noktaları sil, virgülü noktaya çevir
        // Eğer sadece nokta varsa ve ondalık kısımsa veya binlik kısımsa -> duruma göre ele almak lazım.
        // Güvenli yöntem: Tüm noktaları sil, sadece virgülü ondalık olarak kabul et. (11.800 -> 11800)
        // Ancak kullanıcı bazen İngilizce formatta "11.8" girebilir.
        // Şöyle yapalım: Son ayırıcıyı bulalım.
        var s = str.trim();
        if(s === "") return 0;
        
        // Eğer string'de virgül varsa, noktaları binlik ayracı sayıp silelim, virgülü ondalık yapalım.
        if (s.indexOf(',') > -1) {
            s = s.replace(/\./g, '').replace(',', '.');
        } else {
            // Eğer string'de nokta varsa ve birden fazlaysa veya 3 haneden sonraysa, bunu binlik yap.
            // En iyisi Excel mantığı: Virgül ondalıktır. Nokta varsa binlik ayracıdır.
            // Ama kullanıcı ondalık için nokta kullanıyorsa? (Örn 1.23)
            // Noktadan sonra 2 hane veya 1 hane varsa genelde ondalıktır.
            // Biz basitçe virgülü ondalık nokta yapalım. Noktaları silelim.
            // Ancak kullanıcı "1.23" yazarsa bu 123 olur! 
            // O yüzden nokta varsa ve virgül yoksa, noktanın sonuncu olduğu duruma bakalım.
            var lastDot = s.lastIndexOf('.');
            if (lastDot > -1) {
                // Eğer 0. ile başlıyorsa kesin ondalıktır, veya noktadan sonra 1-2 hane varsa ondalıktır
                if (s.startsWith('0.') || s.startsWith('-0.') || s.length - lastDot <= 3) {
                    // Ondalık olarak bırak
                } else {
                    // Binlik ayracı olarak kabul et ve sil
                    s = s.replace(/\./g, '');
                }
            }
        }
        return parseFloat(s) || 0;
    }

    // MRP Hesaplamaları
function calculateMRP() {
    var hasMissing = false;

    document.querySelectorAll('.color-pane').forEach(function(pane) {
        var siparisAdetiEl = pane.querySelector('.mrp-siparis-adeti');
        if(!siparisAdetiEl) return;
        
        var siparisAdeti = parseTurkishFloat(siparisAdetiEl.value || "0");
        var bedenMiktarEl = pane.querySelector('.mrp-beden-miktar');
        var bedenMiktar = parseTurkishFloat(bedenMiktarEl ? bedenMiktarEl.value : "0");
        var bedenCikacakEl = pane.querySelector('.mrp-beden-cikacak');
        var bedenCikacak = parseTurkishFloat(bedenCikacakEl ? bedenCikacakEl.value : "0");
        
        var istenenMetrajEl = pane.querySelector('.mrp-istenen-metraj');
        var istenenKumasMiktarEl = pane.querySelector('.mrp-istenen-kumas-miktar');
        
        // Ihtiyac Olan Kumas satirindaki degerler modelden sabit geldigi icin her zaman otomatik hesapla
        if (istenenKumasMiktarEl && istenenMetrajEl) {
            var istenenMetraj = parseTurkishFloat(istenenMetrajEl.value || "1");
            istenenKumasMiktarEl.value = Math.round(siparisAdeti * istenenMetraj).toLocaleString('tr-TR');
        }

        var jobQty = bedenMiktar > 0 ? bedenCikacak : siparisAdeti;

        pane.querySelectorAll('.mrp-row').forEach(function(row) {
            var birimInput = row.querySelector('.row-birim');
            var isAdetiEl = row.querySelector('.row-is-adeti');
            var toplamEl = row.querySelector('.row-toplam');
            var fireInput = row.querySelector('.row-fire');
            var fazlaEl = row.querySelector('.row-fazla');
            var istenenEl = row.querySelector('.row-istenen');
            var gelenInput = row.querySelector('.row-gelen');
            var artiEksiEl = row.querySelector('.row-arti-eksi');

            var birim = parseTurkishFloat(birimInput ? birimInput.value : "0");
            var fire = parseTurkishFloat(fireInput ? fireInput.value : "0");
            var gelen = gelenInput ? parseTurkishFloat(gelenInput.value || "0") : 0;

            if(isAdetiEl) isAdetiEl.innerText = Math.round(jobQty).toLocaleString('tr-TR');

            var toplam = jobQty * birim;
            if(toplamEl) toplamEl.innerText = Math.round(toplam).toLocaleString('tr-TR');

            var fazla = Math.round(toplam * (fire / 100));
            if(fazlaEl) fazlaEl.innerText = fazla.toLocaleString('tr-TR');

            var istenen = Math.round(toplam + fazla);
            if(istenenEl) istenenEl.innerText = istenen.toLocaleString('tr-TR');

            if (gelenInput) {
                var artiEksi = gelen - istenen;
                if(artiEksi < 0) hasMissing = true;

                if(artiEksiEl) {
                    artiEksiEl.innerText = Math.round(artiEksi).toLocaleString('tr-TR');
                    if(artiEksi < 0) artiEksiEl.style.color = '#dc2626';
                    else if (artiEksi > 0) artiEksiEl.style.color = '#16a34a';
                    else artiEksiEl.style.color = '#0f172a';
                }
            }
        });

        pane.querySelectorAll('.interlining-row').forEach(function(row) {
            var isMiktariEl = row.querySelector('.row-is-miktari');
            var birimInput = row.querySelector('.row-tela-birim');
            var ihtiyacEl = row.querySelector('.row-ihtiyac');

            var birim = parseTurkishFloat(birimInput ? birimInput.value : "0");

            if(isMiktariEl) isMiktariEl.innerText = Math.round(jobQty).toLocaleString('tr-TR');

            var ihtiyac = Math.ceil(jobQty * birim);
            if(ihtiyacEl) ihtiyacEl.innerText = ihtiyac.toLocaleString('tr-TR');
        });

        var labelTable = pane.querySelector('.mrp-label-table');
        if(labelTable) {
            var asortiOranInputs = labelTable.querySelectorAll('.label-asorti-oran');
            var acikAdetInputs = labelTable.querySelectorAll('.label-acik-adet');
            var sipAdetiInput = labelTable.querySelector('.label-sip-adeti');
            var toplamBedenTds = labelTable.querySelectorAll('.label-toplam-beden');
            
            var totalAsortiOran = 0;
            var totalAcikAdet = 0;
            
            asortiOranInputs.forEach(input => totalAsortiOran += parseTurkishFloat(input.value || "0"));
            acikAdetInputs.forEach(input => totalAcikAdet += parseTurkishFloat(input.value || "0"));
            
            var sipAdeti = parseTurkishFloat(sipAdetiInput ? sipAdetiInput.value : "0");
            
            if(labelTable.querySelector('.label-asorti-oran-toplam')) labelTable.querySelector('.label-asorti-oran-toplam').innerText = totalAsortiOran.toLocaleString('tr-TR');
            if(labelTable.querySelector('.label-sip-adeti-toplam')) labelTable.querySelector('.label-sip-adeti-toplam').innerText = sipAdeti.toLocaleString('tr-TR');
            if(labelTable.querySelector('.label-acik-adet-toplam')) labelTable.querySelector('.label-acik-adet-toplam').innerText = totalAcikAdet.toLocaleString('tr-TR');
            
            var birimAsorti = totalAsortiOran > 0 ? totalAsortiOran : 1;
            var kalanAdet = jobQty - totalAcikAdet;
            var asortiMultiplier = Math.round(kalanAdet / birimAsorti);
            if (asortiMultiplier < 0) asortiMultiplier = 0;
            
            if(labelTable.querySelector('.label-kalan-adet-val')) labelTable.querySelector('.label-kalan-adet-val').innerText = kalanAdet.toLocaleString('tr-TR');
            if(labelTable.querySelector('.label-yeni-lot-val')) labelTable.querySelector('.label-yeni-lot-val').innerText = asortiMultiplier.toLocaleString('tr-TR', {maximumFractionDigits: 3});
            
            var sizeBaseTotals = {};
            var genelToplam = 0;
            
            if (toplamBedenTds.length > 0) {
                toplamBedenTds.forEach(td => {
                    var size = td.getAttribute('data-size');
                    var asortiO = 0;
                    var acikQ = 0;
                    asortiOranInputs.forEach(inp => { if(inp.getAttribute('data-size') === size) asortiO = parseTurkishFloat(inp.value || "0"); });
                    acikAdetInputs.forEach(inp => { if(inp.getAttribute('data-size') === size) acikQ = parseTurkishFloat(inp.value || "0"); });
                    
                    var totalForSize = (asortiO * asortiMultiplier) + acikQ;
                    sizeBaseTotals[size] = totalForSize;
                    genelToplam += totalForSize;
                    
                    td.innerText = Math.round(totalForSize).toLocaleString('tr-TR');
                });
            } else {
                genelToplam = jobQty;
            }
            
            if(labelTable.querySelector('.label-genel-toplam')) labelTable.querySelector('.label-genel-toplam').innerText = Math.round(genelToplam).toLocaleString('tr-TR');

            labelTable.querySelectorAll('.mrp-label-row').forEach(function(row) {
                var labelFireInput = row.querySelector('.row-label-fire');
                var labelFire = parseTurkishFloat(labelFireInput ? labelFireInput.value : "0");
                
                var rowTotal = 0;
                var sizeTds = row.querySelectorAll('.row-label-size-qty');
                
                if (sizeTds.length > 0) {
                    sizeTds.forEach(function(td) {
                        var size = td.getAttribute('data-size');
                        var baseTotal = sizeBaseTotals[size] || 0;
                        
                        var finalSizeTotal = baseTotal + (baseTotal * (labelFire / 100));
                        var roundedTotal = Math.round(finalSizeTotal);
                        
                        td.innerText = roundedTotal.toLocaleString('tr-TR');
                        rowTotal += roundedTotal;
                    });
                } else {
                    var finalTotal = genelToplam + (genelToplam * (labelFire / 100));
                    rowTotal = Math.round(finalTotal);
                }
                
                var toplamEl = row.querySelector('.row-label-toplam');
                if(toplamEl) toplamEl.innerText = rowTotal.toLocaleString('tr-TR');
            });
        }
    });

    var missingInput = document.getElementById('pur_has_missing_materials');
    if(missingInput) missingInput.value = hasMissing ? "true" : "false";
}

var colorTabsContainer = document.getElementById('colorTabsContainer');
if(colorTabsContainer) colorTabsContainer.addEventListener('click', function(e) {
    if(e.target.closest('.color-tab-add')) {
        var modalHtml = `
        <div id="customColorModal" style="position:fixed; top:0; left:0; width:100%; height:100%; background:rgba(15,23,42,0.6); backdrop-filter:blur(4px); z-index:9999; display:flex; align-items:center; justify-content:center;">
            <div style="background:white; padding:24px; border-radius:12px; width:400px; box-shadow:0 20px 25px -5px rgba(0,0,0,0.1);">
                <h3 style="margin:0 0 16px 0; color:var(--primary-color); font-size:1.2rem; display:flex; align-items:center; gap:8px;"><i class="fa-solid fa-palette"></i> Yeni Renk Ekle</h3>
                <label style="display:block; margin-bottom:8px; font-weight:600; color:#475569;">Renk Adı:</label>
                <input type="text" id="newColorName" class="form-control" placeholder="Örn: BLACK, NAVY" style="width:100%; margin-bottom:16px; box-sizing:border-box;" autocomplete="off" />
                
                <label style="display:flex; align-items:center; gap:8px; margin-bottom:24px; cursor:pointer; font-size:0.9rem; color:#475569;">
                    <input type="checkbox" id="copyFromCurrent" checked style="width:16px; height:16px;" />
                    Mevcut renkteki tüm bilgileri kopyala
                </label>
                
                <div style="display:flex; justify-content:flex-end; gap:12px;">
                    <button type="button" id="btnCancelColor" class="btn-secondary" style="padding:8px 16px;">İptal</button>
                    <button type="button" id="btnSaveColor" class="btn-planner" style="padding:8px 20px;"><i class="fa-solid fa-check" style="margin-right:6px;"></i> Ekle</button>
                </div>
            </div>
        </div>
        `;
        document.body.insertAdjacentHTML('beforeend', modalHtml);
        
        var modal = document.getElementById('customColorModal');
        var input = document.getElementById('newColorName');
        input.focus();
        
        var closeAndRemove = function() {
            modal.remove();
        };
        
        document.getElementById('btnCancelColor').onclick = closeAndRemove;
        
        document.getElementById('btnSaveColor').onclick = function() {
            var color = input.value.trim().toUpperCase();
            if(!color) {
                showBeautifulAlert("Lütfen geçerli bir renk adı girin!", "danger");
                return;
            }
            var existing = document.querySelector('.color-tab[data-color="' + color + '"]');
            if(existing) {
                showBeautifulAlert("Bu renk zaten var!", "danger");
                return;
            }
            
            var baseColor = null;
            if(document.getElementById('copyFromCurrent').checked) {
                var activeTab = document.querySelector('.color-tab.active');
                if(activeTab) {
                    baseColor = activeTab.getAttribute('data-color');
                }
            }
            
            closeAndRemove();
            
            var paneToClone = document.querySelector('.color-pane.active') || document.querySelector('.color-pane');
            if(!paneToClone) return;
            
            var newPane = paneToClone.cloneNode(true);
            newPane.id = "pane-" + color;
            newPane.classList.remove("active");
            
            var sourceColor = paneToClone.id.replace("pane-", "");
            
            newPane.querySelectorAll('input, select, textarea').forEach(function(input) {
                if(input.name) {
                    input.name = input.name.replace("pur_color_" + sourceColor + "_", "pur_color_" + color + "_");
                }
                if(!baseColor) {
                    if(input.type === "text" || input.type === "number") input.value = "0";
                    else if(input.type === "date") input.value = "";
                }
            });
            
            // replace color names in titles
            var titles = newPane.querySelectorAll('.section-title');
            titles.forEach(function(t) {
                t.innerHTML = t.innerHTML.replace(sourceColor, color);
            });
            var labels = newPane.querySelectorAll('.data-label');
            labels.forEach(function(t) {
                t.innerHTML = t.innerHTML.replace(sourceColor, color);
            });
            
            document.getElementById('colorPanesContainer').appendChild(newPane);
            
            var newTab = document.createElement('div');
            newTab.className = "color-tab";
            newTab.setAttribute("data-color", color);
            newTab.innerText = color;
            
            document.getElementById('btnAddColor').before(newTab);
            
            newTab.click();
            calculateMRP();
        };
        return;
    }
    
    var tab = e.target.closest('.color-tab');
    if(tab) {
        document.querySelectorAll('.color-tab').forEach(t => t.classList.remove('active'));
        document.querySelectorAll('.color-pane').forEach(p => p.classList.remove('active'));
        
        tab.classList.add('active');
        document.getElementById('pane-' + tab.getAttribute('data-color')).classList.add('active');
    }
});

// Event delegation for dynamically added inputs
var colorPanesContainer = document.getElementById('colorPanesContainer');
if(colorPanesContainer) colorPanesContainer.addEventListener('input', function(e) {
    if (e.target.classList.contains('mrp-calc-source-adet')) {
        var tr = e.target.closest('tr');
        if (tr) {
            var adet = parseTurkishFloat(e.target.value);
            var metrajEl = tr.querySelector('.mrp-calc-source-metraj');
            var kumasEl = tr.querySelector('.mrp-calc-source-kumas');
            if (metrajEl && kumasEl) {
                var metraj = parseTurkishFloat(metrajEl.value);
                kumasEl.value = Math.round(adet * metraj).toLocaleString('tr-TR');
            }
        }
    } else if (e.target.classList.contains('mrp-calc-source-kumas')) {
        var tr = e.target.closest('tr');
        if (tr) {
            var kumas = parseTurkishFloat(e.target.value);
            var metrajEl = tr.querySelector('.mrp-calc-source-metraj');
            var adetEl = tr.querySelector('.mrp-calc-source-adet');
            
            if (adetEl && metrajEl) {
                var adet = parseTurkishFloat(adetEl.value);
                // Siparis adeti sabit oldugu icin, kumas miktari degisirse metraj hesaplanir.
                // Eldeki kumas satirinda (Kumas 2) ise, adet hesaplanabilir, ama "Siparis Adeti" readonly yapildi.
                // Eger Kumas 2 ise adet hesaplanmali, Ihtiyac Olan Kumas ise metraj hesaplanmali.
                if (adetEl.hasAttribute('readonly') && adetEl.classList.contains('mrp-siparis-adeti')) {
                    if (adet > 0) {
                        var hesaplananMetraj = kumas / adet;
                        metrajEl.value = Number.isInteger(hesaplananMetraj) ? hesaplananMetraj.toString() : hesaplananMetraj.toFixed(3);
                    }
                } else {
                    var metraj = parseTurkishFloat(metrajEl.value);
                    if (metraj > 0) {
                        adetEl.value = Math.round(kumas / metraj).toLocaleString('tr-TR');
                    }
                }
            }
        }
    } else if (e.target.classList.contains('mrp-calc-source-metraj')) {
        var tr = e.target.closest('tr');
        if (tr) {
            var metraj = parseTurkishFloat(e.target.value);
            var adetEl = tr.querySelector('.mrp-calc-source-adet');
            var kumasEl = tr.querySelector('.mrp-calc-source-kumas');
            if (adetEl && kumasEl) {
                var adet = parseTurkishFloat(adetEl.value);
                kumasEl.value = Math.round(adet * metraj).toLocaleString('tr-TR');
            }
        }
    }

    if(e.target.classList.contains('mrp-input') || e.target.classList.contains('interlining-input')) {
        calculateMRP();
    }
});

    // Güzel bir uyarı ve onay penceresi (SweetAlert benzeri)
    function showBeautifulAlert(message, type = 'warning') {
        $("#beautifulAlertOverlay").remove();
        var icon = type === 'warning' ? '<i class="fa-solid fa-triangle-exclamation" style="font-size:3rem; color:#eab308; margin-bottom:15px;"></i>' : '<i class="fa-solid fa-circle-exclamation" style="font-size:3rem; color:#dc2626; margin-bottom:15px;"></i>';
        var overlay = $('<div id="beautifulAlertOverlay" style="position:fixed; top:0; left:0; width:100%; height:100%; background:rgba(15, 23, 42, 0.6); z-index:99999; display:flex; align-items:center; justify-content:center; backdrop-filter:blur(4px); opacity:0; transition:opacity 0.3s;">' +
            '<div style="background:#fff; padding:30px 40px; border-radius:16px; box-shadow:0 25px 50px -12px rgba(0,0,0,0.25); text-align:center; max-width:450px; transform:scale(0.9); transition:transform 0.3s cubic-bezier(0.34, 1.56, 0.64, 1);">' +
            icon +
            '<h3 style="margin:0 0 10px 0; color:#1e293b; font-size:1.25rem; font-weight:700;">Uyarı</h3>' +
            '<p style="color:#475569; font-size:1rem; margin-bottom:25px; line-height:1.5;">' + message + '</p>' +
            '<button onclick="$(\'#beautifulAlertOverlay\').css(\'opacity\',\'0\').find(\'div\').css(\'transform\',\'scale(0.9)\'); setTimeout(function(){ $(\'#beautifulAlertOverlay\').remove(); }, 300)" style="background:#2563eb; color:#fff; border:none; padding:10px 24px; border-radius:8px; font-weight:600; cursor:pointer; transition:all 0.2s;" onmouseover="this.style.background=\'#1d4ed8\'" onmouseout="this.style.background=\'#2563eb\'">Tamam, Anladım</button>' +
            '</div></div>');
        $("body").append(overlay);
        // Force reflow and animate
        overlay[0].offsetHeight;
        overlay.css('opacity', '1').find('div').css('transform', 'scale(1)');
    }

    function showBeautifulConfirm(message, onConfirm) {
        $("#beautifulConfirmOverlay").remove();
        var overlay = $('<div id="beautifulConfirmOverlay" style="position:fixed; top:0; left:0; width:100%; height:100%; background:rgba(15, 23, 42, 0.6); z-index:99999; display:flex; align-items:center; justify-content:center; backdrop-filter:blur(4px); opacity:0; transition:opacity 0.3s;">' +
            '<div style="background:#fff; padding:30px 40px; border-radius:16px; box-shadow:0 25px 50px -12px rgba(0,0,0,0.25); text-align:center; max-width:450px; transform:scale(0.9); transition:transform 0.3s cubic-bezier(0.34, 1.56, 0.64, 1);">' +
            '<i class="fa-solid fa-circle-question" style="font-size:3rem; color:#3b82f6; margin-bottom:15px;"></i>' +
            '<h3 style="margin:0 0 10px 0; color:#1e293b; font-size:1.25rem; font-weight:700;">Emin misiniz?</h3>' +
            '<p style="color:#475569; font-size:1rem; margin-bottom:25px; line-height:1.5;">' + message + '</p>' +
            '<div style="display:flex; justify-content:center; gap:12px;">' +
            '<button id="btnConfirmCancel" style="background:#e2e8f0; color:#475569; border:none; padding:10px 20px; border-radius:8px; font-weight:600; cursor:pointer; transition:all 0.2s;" onmouseover="this.style.background=\'#cbd5e1\'" onmouseout="this.style.background=\'#e2e8f0\'">İptal</button>' +
            '<button id="btnConfirmOk" style="background:#2563eb; color:#fff; border:none; padding:10px 20px; border-radius:8px; font-weight:600; cursor:pointer; transition:all 0.2s;" onmouseover="this.style.background=\'#1d4ed8\'" onmouseout="this.style.background=\'#2563eb\'">Evet, Devam Et</button>' +
            '</div></div></div>');
        
        $("body").append(overlay);
        overlay[0].offsetHeight;
        overlay.css('opacity', '1').find('div').css('transform', 'scale(1)');

        var close = function() {
            overlay.css('opacity', '0').find('div').css('transform', 'scale(0.9)');
            setTimeout(function() { overlay.remove(); }, 300);
        };

        $('#btnConfirmCancel').click(function() { close(); });
        $('#btnConfirmOk').click(function() {
            close();
            onConfirm();
        });
    }

const orderId = @Model.Id;

let planCuttingItems = @Html.Raw(string.IsNullOrEmpty(Model.PlannedCuttingJson) ? "[]" : Model.PlannedCuttingJson);
let planTargetSizes = @Html.Raw(string.IsNullOrEmpty(Model.SizeDistributionJson) ? "{}" : Model.SizeDistributionJson);
let planTargetSizesAsorti = @Html.Raw(string.IsNullOrEmpty(Model.AsortiDistributionJson) ? "{}" : Model.AsortiDistributionJson);
const planAsortiCount = @Model.AsortiCount;

function planGetTotalCutForSize(sizeName, cutType) {
    let total = 0;
    planCuttingItems.forEach(item => {
        if (item.CutType === cutType && item.Sizes && item.Sizes[sizeName]) {
            total += parseFloat(item.Sizes[sizeName]) || 0;
        }
    });
    return total;
}
// --- TAB 3: KESİM SÜRECİ ---
        function planToggleCutForm(type) {
            if (type === 'AcikAdet') {
                $('#planCuttingEntryAcik').show();
                planCalculateAcik();
                setTimeout(function() { $('#planTxtAcikMultiplier').focus(); }, 100);
            } else {
                $('#planCuttingEntryAsorti').show();
                planCalculateAsorti();
                setTimeout(function() { $('#planTxtAsortiMultiplier').focus(); }, 100);
            }
        }
        
        function planCalculateAcik() {
            const multiplier = parseFloat($('#planTxtAcikMultiplier').val()) || 1;
            const markerLength = parseFloat($('#planTxtAcikMarkerLength').val()) || 0;
            
            let totalQty = 0;
            $('.acik-adet-input-row').each(function() {
                const val = parseFloat($(this).val()) || 0;
                totalQty += (val * multiplier);
            });
            
            $('#planTxtAcikTotal').val(totalQty);
            if (totalQty > 0) {
                $('#planTxtAcikUnitMeterage').val(((markerLength * multiplier) / totalQty).toFixed(2));
            } else {
                $('#planTxtAcikUnitMeterage').val('0');
            }
        }

        function planCalculateAsorti() {
            const multiplier = parseFloat($('#planTxtAsortiMultiplier').val()) || 1;
            const markerLength = parseFloat($('#planTxtAsortiMarkerLength').val()) || 0;
            
            let totalQty = 0;
            $('.asorti-input-row').each(function() {
                const asortiValue = parseFloat($(this).data('asortivalue')) || 0;
                const calculatedValue = asortiValue * multiplier;
                $(this).val(calculatedValue); 
                totalQty += calculatedValue;
            });
            
            $('#planTxtAsortiTotal').val(totalQty);
            if (totalQty > 0) {
                $('#planTxtAsortiUnitMeterage').val(((markerLength * multiplier) / totalQty).toFixed(2));
            } else {
                $('#planTxtAsortiUnitMeterage').val('0');
            }
        }
        
        function planSaveAcikRow() {
            const date = $('#planTxtAcikDate').val();
            const multiplier = parseFloat($('#planTxtAcikMultiplier').val()) || 1;
            const markerLength = parseFloat($('#planTxtAcikMarkerLength').val()) || 0;
            const totalQty = parseFloat($('#planTxtAcikTotal').val()) || 0;
            const unitMeterage = parseFloat($('#planTxtAcikUnitMeterage').val()) || 0;

            if (totalQty <= 0) {
                showBeautifulAlert("Lütfen geçerli bir kesim değeri giriniz (toplam adet > 0 olmalı).", "warning");
                return;
            }

            const sizes = {};
            let validationError = "";
            $('.acik-adet-input-row').each(function() {
                const sizeName = $(this).data('size');
                const val = parseFloat($(this).val()) || 0;
                const calculatedValue = val * multiplier;
                if(calculatedValue > 0) {
                    const targetQty = parseFloat(planTargetSizes[sizeName]) || 0;
                    const alreadyCut = planGetTotalCutForSize(sizeName, 'Açık Adet');
                    if ((alreadyCut + calculatedValue) > targetQty) {
                        validationError = `Hata: '${sizeName}' bedeni için sipariş hedefini aşıyorsunuz! (Hedef: ${targetQty}, Toplam Kesilecek: ${alreadyCut + calculatedValue})`;
                    }
                    sizes[sizeName] = calculatedValue;
                }
            });
            
            if (validationError) {
                Swal.fire({
                    title: 'Uyarı',
                    text: validationError + "\n\nYine de kaydetmek istiyor musunuz?",
                    icon: 'warning',
                    showCancelButton: true,
                    confirmButtonColor: '#3085d6',
                    cancelButtonColor: '#d33',
                    confirmButtonText: 'Evet, Kaydet',
                    cancelButtonText: 'İptal'
                }).then((result) => {
                    if (result.isConfirmed) {
                        _planAddCutToGlobalAndRender('Açık Adet', date, multiplier, sizes, totalQty, markerLength, unitMeterage);
                        // Reset
                        $('.acik-adet-input-row').val('0');
                        $('#planTxtAcikMultiplier').val('1');
                        $('#planCuttingEntryAcik').hide();
                        planCalculateAcik();
                    }
                });
                return;
            }
            
            _planAddCutToGlobalAndRender('Açık Adet', date, multiplier, sizes, totalQty, markerLength, unitMeterage);
            
            // Reset
            $('.acik-adet-input-row').val('0');
            $('#planTxtAcikMultiplier').val('1');
            $('#planCuttingEntryAcik').hide();
            planCalculateAcik();
        }

        function planSaveAsortiRow() {
            const date = $('#planTxtAsortiDate').val();
            const multiplier = parseFloat($('#planTxtAsortiMultiplier').val()) || 1;
            const markerLength = parseFloat($('#planTxtAsortiMarkerLength').val()) || 0;
            const totalQty = parseFloat($('#planTxtAsortiTotal').val()) || 0;
            const unitMeterage = parseFloat($('#planTxtAsortiUnitMeterage').val()) || 0;

            if (totalQty <= 0) {
                showBeautifulAlert("Lütfen geçerli bir kesim değeri giriniz (toplam adet > 0 olmalı).", "warning");
                return;
            }

            const sizes = {};
            let validationError = "";
            $('.asorti-input-row').each(function() {
                const sizeName = $(this).data('size');
                const asortiValue = parseFloat($(this).data('asortivalue')) || 0;
                const val = asortiValue * multiplier;
                if(val > 0) {
                    const targetQty = (parseFloat(planTargetSizesAsorti[sizeName]) || 0) * planAsortiCount;
                    const alreadyCut = planGetTotalCutForSize(sizeName, 'Asorti');
                    if ((alreadyCut + val) > targetQty) {
                        validationError = `Hata: '${sizeName}' bedeni için sipariş hedefini aşıyorsunuz! (Hedef: ${targetQty}, Toplam Kesilecek: ${alreadyCut + val})`;
                    }
                    sizes[sizeName] = val;
                }
            });
            
            if (validationError) {
                Swal.fire({
                    title: 'Uyarı',
                    text: validationError + "\n\nYine de kaydetmek istiyor musunuz?",
                    icon: 'warning',
                    showCancelButton: true,
                    confirmButtonColor: '#3085d6',
                    cancelButtonColor: '#d33',
                    confirmButtonText: 'Evet, Kaydet',
                    cancelButtonText: 'İptal'
                }).then((result) => {
                    if (result.isConfirmed) {
                        _planAddCutToGlobalAndRender('Asorti', date, multiplier, sizes, totalQty, markerLength, unitMeterage);
                        // Reset
                        $('#planTxtAsortiMultiplier').val('1');
                        $('#planCuttingEntryAsorti').hide();
                        planCalculateAsorti();
                    }
                });
                return;
            }
            
            _planAddCutToGlobalAndRender('Asorti', date, multiplier, sizes, totalQty, markerLength, unitMeterage);
            
            // Reset
            $('#planTxtAsortiMultiplier').val('1');
            $('#planCuttingEntryAsorti').hide();
            planCalculateAsorti();
        }

        function _planAddCutToGlobalAndRender(cutType, date, multiplier, sizes, totalQty, markerLength, unitMeterage) {
            const newCutName = "Kesim " + (planCuttingItems.length + 1);

            planCuttingItems.push({
                CutName: newCutName,
                CutType: cutType,
                Date: date,
                Multiplier: multiplier,
                Sizes: sizes,
                TotalQuantity: totalQty,
                MarkerLength: markerLength,
                UnitMeterage: unitMeterage
            });

            planRenderCuttingTable();
        }

        function planRemoveCuttingItem(index) {
            planCuttingItems.splice(index, 1);
            
            // Numaraları Yeniden Sırala
            planCuttingItems.forEach((item, idx) => {
                item.CutName = "Kesim " + (idx + 1);
            });
            
            planRenderCuttingTable();
            planSaveCuttingData();
        }

        function planRenderCuttingTable() {
            let htmlAcik = '';
            let htmlAsorti = '';
            let acikTotal = 0;
            let asortiTotal = 0;
            let grandTotal = 0;
            
            let acikSizeTotals = {};
            let asortiSizeTotals = {};
            
            planCuttingItems.forEach((item, index) => {
                let cutName = item.CutName || `Kesim ${index + 1}`;
                let cutTypeLabel = item.CutType || 'Açık Adet';
                let isAsorti = cutTypeLabel === 'Asorti';
                let sizesArr = isAsorti ? (typeof asortiSizesArr !== 'undefined' ? asortiSizesArr : []) : (typeof acikSizesArr !== 'undefined' ? acikSizesArr : []);
                
                let sizesHtml = '';
                if (sizesArr && sizesArr.length > 0) {
                    sizesArr.forEach(s => {
                        let val = parseFloat(item.Sizes && item.Sizes[s] ? item.Sizes[s] : 0) || 0;
                        if (isAsorti) {
                            asortiSizeTotals[s] = (asortiSizeTotals[s] || 0) + val;
                        } else {
                            acikSizeTotals[s] = (acikSizeTotals[s] || 0) + val;
                        }
                        
                        let displayVal = val;
                        if (item.Multiplier && item.Multiplier > 0) {
                            displayVal = val / item.Multiplier;
                        }
                        if (displayVal % 1 !== 0) {
                            displayVal = displayVal.toFixed(2);
                        }
                        sizesHtml += `<td style="vertical-align: middle; text-align: center; color: #475569; font-weight: 500;">${displayVal}</td>`;
                    });
                } else {
                    sizesHtml = `<td style="vertical-align: middle; text-align: center; color: #475569;">-</td>`;
                }
                
                let qty = item.TotalQuantity || 0;
                grandTotal += qty;
                
                let rowHtml = `
                    <tr style="background: white; text-align: center;">
                        <td class="sticky-kesim" style="font-weight: 700; color: #1e293b; vertical-align: middle; position: sticky; left: 0; background: white; z-index: 2; box-shadow: 2px 0 5px -2px rgba(0,0,0,0.1);">${cutName}</td>
                        <td style="vertical-align: middle;">${item.Multiplier}</td>
                        ${sizesHtml}
                        <td style="vertical-align: middle;"><strong style="color: #0f172a; font-size: 1.1rem;">${qty}</strong></td>
                        <td style="vertical-align: middle;">${item.MarkerLength}</td>
                        <td style="vertical-align: middle;">${item.UnitMeterage}</td>
                        <td style="vertical-align: middle;">${item.Date}</td>
                        <td class="sticky-islem" style="vertical-align: middle; position: sticky; right: 0; background: white; z-index: 2; box-shadow: -2px 0 5px -2px rgba(0,0,0,0.1);"><button class="btn btn-sm btn-danger" onclick="planRemoveCuttingItem(${index})"><i class="fa-solid fa-trash"></i></button></td>
                    </tr>
                `;
                
                if (isAsorti) {
                    htmlAsorti += rowHtml;
                    asortiTotal += qty;
                } else {
                    htmlAcik += rowHtml;
                    acikTotal += qty;
                }
            });
            
            $('#planCuttingBodyAcik').html(htmlAcik);
            $('#planAcikTotalCut').text(acikTotal.toLocaleString('tr-TR'));
            
            // Açık Adet Beden Toplamlarını Güncelle
            if (typeof acikSizesArr !== 'undefined') {
                acikSizesArr.forEach(s => {
                    let totalVal = acikSizeTotals[s] || 0;
                    let displayTotal = totalVal % 1 !== 0 ? totalVal.toFixed(2) : totalVal;
                    $('#planAcikTotalCutSize_' + s).text(displayTotal);
                });
            }
            
            $('#planCuttingBodyAsorti').html(htmlAsorti);
            $('#planAsortiTotalCut').text(asortiTotal.toLocaleString('tr-TR'));
            
            // Asorti Beden Toplamlarını Güncelle
            if (typeof asortiSizesArr !== 'undefined') {
                asortiSizesArr.forEach(s => {
                    let totalVal = asortiSizeTotals[s] || 0;
                    let displayTotal = totalVal % 1 !== 0 ? totalVal.toFixed(2) : totalVal;
                    $('#planAsortiTotalCutSize_' + s).text(displayTotal);
                });
            }
            
            $('#planGrandTotalCut').text(grandTotal.toLocaleString('tr-TR'));

            // Sipariş limit kontrolü
            let orderQty = @Model.CalculatedQuantity;
            let remaining = orderQty - grandTotal;
            if (remaining > 0) {
                $('#planDivRemainingCut').css('color', '#166534').html(`Kalan İhtiyaç: <strong id="planLblRemainingQty">${remaining}</strong> Adet`);
                $('#planBtnEkleKesim').prop('disabled', false).removeClass('btn-secondary').addClass('btn-primary').html('<i class="fa-solid fa-plus" style="margin-right:4px;"></i> Kesim Ekle');
            } else {
                let excess = Math.abs(remaining);
                let text = excess > 0 ? `Hedef Tamamlandı! (Fazla: ${excess} Adet)` : `Hedef Tamamlandı!`;
                $('#planDivRemainingCut').css('color', '#15803d').html(`<i class="fa-solid fa-circle-check" style="margin-right: 5px;"></i> <strong>${text}</strong>`);
                $('#planBtnEkleKesim').prop('disabled', true).removeClass('btn-primary').addClass('btn-secondary').html('<i class="fa-solid fa-lock" style="margin-right:4px;"></i> Kilitli');
            }
        }

        function planSaveCuttingData() {
            $.ajax({
                url: '/Planning/UpdatePlannedCutting',
                type: 'POST',
                contentType: 'application/json',
                data: JSON.stringify({ Id: orderId, PlannedCuttingJson: JSON.stringify(planCuttingItems) }),
                success: function(res) {
                    if(res.success) showBeautifulAlert("Kesim bilgileri kaydedildi.", "success");
                    else showBeautifulAlert("Hata: " + res.message, "danger");
                }
            });
        }

        
    document.addEventListener('DOMContentLoaded', function() {
        planRenderCuttingTable();
        calculateMRP();

        // Sipariş listesi başlangıç filtresi: Aktif
        var currentOrderFilterInit = 'active';
        currentOrderFilter = currentOrderFilterInit;
        filterOrders();

        // Aktif siparişi listede görünür alana kaydır
        var activeOrder = document.querySelector('.order-list-item.active');
        if (activeOrder) {
            activeOrder.scrollIntoView({ behavior: 'auto', block: 'center' });
        }

    function bindCompletionValidation(formId, checkboxName, phaseName) {
        var form = document.getElementById(formId);
        if (!form) return;
        var checkbox = form.querySelector('input[name="' + checkboxName + '"]');
        if (!checkbox) return;
        
        function checkEmptyFields() {
            var emptyFields = false;
            var inputs = form.querySelectorAll('input:not([type="hidden"]):not([readonly]):not([type="checkbox"]):not(:disabled):not(.not-mandatory), select:not(:disabled):not(.not-mandatory)');
            for(var i=0; i<inputs.length; i++) {
                // Sadece ekranda görünür olan alanları zorunlu tut
                if (inputs[i].offsetWidth > 0 || inputs[i].offsetHeight > 0) {
                    if (inputs[i].value.trim() === '') {
                        emptyFields = true;
                        inputs[i].style.border = '2px solid #ef4444'; // Hatalı alanı kırmızı ile işaretle
                    } else {
                        inputs[i].style.border = ''; // Dolu olanın işaretini kaldır
                    }
                }
            }
            return emptyFields;
        }

        checkbox.addEventListener('change', function(e) {
            if (this.checked) {
                if (checkEmptyFields()) {
                    showBeautifulAlert("Tüm alanlar doldurulmadan " + phaseName + " tamamlandı olarak işaretlenemez. Lütfen boş alanları doldurunuz.");
                    this.checked = false;
                }
            }
        });

        form.addEventListener('submit', function(e) {
            var isCompleted = checkbox.checked;
            if (isCompleted) {
                if (checkEmptyFields()) {
                    showBeautifulAlert("Tüm alanlar doldurulmadan " + phaseName + " tamamlandı olarak işaretlenemez. Plan kaydedilecek ancak tamamlandı işareti kaldırılacaktır.");
                    checkbox.checked = false;
                    isCompleted = false;
                }
            }
            
            // Satın alma formu için özel kontrol
            if (formId === 'purchasing-form') {
                var hasMissingEl = document.getElementById('pur_has_missing_materials');
                var hasMissing = hasMissingEl ? hasMissingEl.value === "true" : false;
                if (hasMissing && isCompleted && !form.dataset.confirmed) {
                    e.preventDefault();
                    showBeautifulConfirm("Dikkat: Bu siparişte eksik malzemeler bulunmaktadır. Yine de Satın Alma aşamasını tamamlanmış olarak işaretlemek istiyor musunuz?", function() {
                        form.dataset.confirmed = 'true';
                        form.submit();
                    });
                }
            }
        });
    }

    bindCompletionValidation('sample-form', 'IsSampleTestCompleted', 'Numune Planlama');
    bindCompletionValidation('purchasing-form', 'IsPurchasingCompleted', 'Satın Alma Planlama');
    bindCompletionValidation('production-form', 'IsProductionCompleted', 'Üretim Planlama');
});
</script>
