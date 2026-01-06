import openpyxl

wb = openpyxl.load_workbook(r'c:\Users\forst\repo\Ductolator\excel\Mixed Air Calculator 1.xlsx', data_only=False)
sheet = wb.active
print(f'Sheet: {sheet.title}')
print(f'Dimensions: {sheet.dimensions}')
print('\n=== All cells with values ===\n')

for row in sheet.iter_rows(min_row=1, max_row=40, min_col=1, max_col=15):
    for cell in row:
        if cell.value is not None:
            val = str(cell.value)
            if val.startswith('='):
                print(f'{cell.coordinate}: FORMULA: {val}')
            else:
                print(f'{cell.coordinate}: {val}')
