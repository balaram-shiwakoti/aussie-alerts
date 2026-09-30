import { alertSchema } from '../forms';
const valid = { suburb:'', postcode:'0800', minPrice:100, maxPrice:200, minBedrooms:2, propertyType:'unit', active:true };
it('keeps leading zero postcodes', () => expect(alertSchema.parse(valid).postcode).toBe('0800'));
it('rejects no location and inverted prices', () => {
  expect(alertSchema.safeParse({...valid, postcode:''}).success).toBe(false);
  expect(alertSchema.safeParse({...valid, maxPrice:99}).success).toBe(false);
});
