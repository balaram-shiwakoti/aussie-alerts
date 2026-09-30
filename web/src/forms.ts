import { z } from 'zod';
export const propertyTypes = ['house', 'unit', 'townhouse', 'land'] as const;
export const alertSchema = z.object({
  suburb: z.string().trim().max(80),
  postcode: z.string().regex(/^(|[0-9]{4})$/, 'Use four digits, such as 0800.'),
  minPrice: z.number().min(0).max(100_000_000),
  maxPrice: z.number().min(0).max(100_000_000),
  minBedrooms: z.number().int().min(0).max(20),
  propertyType: z.enum(['', ...propertyTypes]), active: z.boolean(),
}).refine(x => x.suburb.length > 0 || x.postcode.length > 0, { message: 'Enter a suburb or postcode.', path: ['suburb'] })
  .refine(x => x.maxPrice >= x.minPrice, { message: 'Maximum must be at least minimum.', path: ['maxPrice'] });
export type AlertValues = z.infer<typeof alertSchema>;
export const listingSchema = z.object({
  title: z.string().trim().min(1).max(150), suburb: z.string().trim().min(1).max(80),
  postcode: z.string().regex(/^[0-9]{4}$/, 'Use four digits.'),
  price: z.number().min(0).max(100_000_000), bedrooms: z.number().int().min(0).max(20),
  propertyType: z.enum(propertyTypes),
});
export type ListingValues = z.infer<typeof listingSchema>;
export function authSchema(register: boolean) {
  return z.object({ email: z.string().trim().email(), password: register
    ? z.string().min(12).max(128).regex(/[a-z]/, 'Add a lowercase letter.').regex(/[A-Z]/, 'Add an uppercase letter.').regex(/[0-9]/, 'Add a number.')
    : z.string().min(1).max(128) });
}
