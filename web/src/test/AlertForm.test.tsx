import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { AlertForm } from '../pages/AlertsPage';
import { api } from '../api/client';
vi.mock('../api/client', () => ({api: {createAlert: vi.fn(), updateAlert: vi.fn()}}));
it('blocks missing location without calling the API', async () => {
  render(<QueryClientProvider client={new QueryClient()}><AlertForm done={() => {}}/></QueryClientProvider>);
  await userEvent.click(screen.getByRole('button', {name:'Save alert'}));
  expect(await screen.findByRole('alert')).toHaveTextContent('Enter a suburb or postcode.');
  expect(api.createAlert).not.toHaveBeenCalled();
});
it('submits a valid postcode and turns optional blank fields into null', async () => {
  vi.mocked(api.createAlert).mockResolvedValue({id:'test', suburb:null, postcode:'0800', minPrice:300000, maxPrice:800000, minBedrooms:2, propertyType:null, active:true, activeSince:'2026-01-01T00:00:00Z'});
  render(<QueryClientProvider client={new QueryClient()}><AlertForm done={() => {}}/></QueryClientProvider>);
  await userEvent.type(screen.getByLabelText('Postcode'), '0800');
  await userEvent.click(screen.getByRole('button', {name:'Save alert'}));
  expect(api.createAlert).toHaveBeenCalledWith(expect.objectContaining({postcode:'0800', suburb:null, propertyType:null}));
});
